# Door Usage Report

A console command that queries your Feenics/acre Security event history for a specific door
(reader/portal) and lists everyone who used it within a given time window, along with the email
address on their profile.

## Usage

```
FeenicsCsvImport.exe door-usage-report [--door "Side Entrance"] [--months 9] [--out report.csv] [--include-denied]
```

It authenticates the same way the CSV sync job does, using these environment variables:

| Variable | Required | Description |
|---|---|---|
| `ACRE_INSTANCE` | Yes | Your Feenics/acre instance name |
| `ACRE_USER` | Yes | Username |
| `ACRE_PASS` | Yes | Password |
| `ACRE_API_URL` | No | Defaults to `https://api.us.acresecurity.cloud` |

### Options

| Flag | Default | Description |
|---|---|---|
| `--door <text>` | `Side Entrance` | Case-insensitive substring matched against the door/reader name |
| `--months <n>` | `9` | How far back to look |
| `--out <path>` | *(none)* | Also write the results to a CSV file |
| `--include-denied` | off | Include denied/failed attempts, not just granted access |
| `--dump-events <n>` | *(none)* | Diagnostic mode — prints the N most recent raw events as JSON instead of running the report |
| `--dump-events-days <n>` | `30` | How far back `--dump-events` looks. Narrowing this keeps the query cheap on a large Events collection (see below); widen it if your last event was longer ago than this |

## First run: verify field names with `--dump-events`

The report queries the Feenics event-history API directly (`aggregate/Events`) rather than through
a documented SDK helper, because the exact field names for reader/door and event type could not be
confirmed against your specific instance when this tool was written. **Before trusting the report,
run it once in diagnostic mode:**

```
FeenicsCsvImport.exe door-usage-report --dump-events 5
```

This prints the 5 most recent events as raw JSON. Confirm that:

- The timestamp field is `OccurredOn` (used for the date-range filter)
- The door/reader name appears somewhere the tool checks: `MessageLong`, `EventData.Reader.CommonName`, or `EventData.Reader.Name`
- Each event's `ObjectLinks` array contains an entry whose key (`LinkedObjectKey`, `LinkedObjectId`, `ObjectId`, or `Key`) matches a person's `Key` from your People list

If your instance uses different field names, adjust `BuildMatchStage` and `ExtractLinkedKey` in
`FeenicsCsvImport.ClassLibrary/DoorUsageReportService.cs` accordingly.

### Notes learned from a live instance

- The `aggregate/Events` endpoint expects each pipeline stage as an individually JSON-encoded
  *string* inside the outer array, not a raw nested object. The code already does this.
- Sorting/limiting the Events collection with no date filter first can trigger a server-side
  `MongoExecutionTimeoutException` — the Events collection can be large. Both `--dump-events` and
  the main report always put a date `$match` first in the pipeline for this reason.

## Output

```
Name                           Email                               Last Used (UTC)   Events
Jane Doe                       jane.doe@example.com               2026-08-30 14:12        6
John Smith                     john.smith@example.com             2026-07-02 09:47        2

Total: 2 distinct people, 8 matching event(s) scanned.
```

People with no email address on file show `(no email on file)` instead of an email.
