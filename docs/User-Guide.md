# User Guide

## Prerequisites

- Windows with .NET Framework 4.8 installed
- A Feenics Keep cloud instance
- A user account with permission to create/update people and assign access levels
- A CSV file with member data (or use **Export Template** to create one)

## Launching the Application

Run `FeenicsCsvImport.Gui.exe`. On first launch, the application loads default settings including three sample access level rules. All settings (except password) are automatically saved between sessions.

## Connection Settings

| Field | Description |
|---|---|
| **API URL** | The Feenics API endpoint. Default: `https://api.us.acresecurity.cloud` |
| **Instance** | Your Feenics instance name (e.g., `OaksLanding`) |
| **Username** | Your Feenics login username |
| **Password** | Your Feenics login password (never saved to disk) |

### Duplicate Handling

When a person in the CSV matches an existing person in Feenics (by name), you can choose:

| Option | Behavior |
|---|---|
| **Skip** | Leave the existing person untouched. Access levels are **not** assigned. This is the default. |
| **Update existing** | Overwrite the existing person's address, phone, and email with values from the CSV. Access levels are assigned. |
| **Create new (may duplicate)** | Always create a new person record, even if one already exists. A confirmation dialog warns you before proceeding. |

Click **Save Settings** to persist your connection details and preferences.

## Access Level Rules

The rules grid defines which Feenics access levels to assign and the age range for each:

| Column | Description |
|---|---|
| **Access Level Name** | The exact name of a Feenics access level |
| **Start Age** | The age when this access level activates |
| **End Age** | The age when it expires. Leave blank (or 0) for permanent access. |
| **Create** | Check this to auto-create the access level in Feenics if it doesn't exist |

### Loading Access Levels from Feenics

1. Fill in your connection settings and password
2. Click **Load from Instance**
3. A picker dialog appears showing all access levels defined in your instance
4. Select the ones you want and click OK
5. They are added to the rules grid with Start Age = 0 and End Age = blank — fill in the appropriate age ranges

### Editing Rules

- Click a cell to edit it
- Use the blank row at the bottom to add new rules
- Select a row and press **Delete** to remove it
- Rules with a blank name are ignored during import

## Preparing the CSV File

### Export a Template

Click **Export Template** next to the Browse button. Choose a save location. The exported CSV contains the correct headers and two sample rows:

```
Name,Address,Phone,Email,Birthday
John Smith,"123 Main St, Springfield, IL 62701",555-123-4567,john.smith@example.com,03/15/2010
Jane Doe,"456 Oak Ave, Columbus, OH 43215",555-987-6543,jane.doe@example.com,07/22/2008
```

Replace the sample rows with your real data and keep the header row.

### Column Details

| Column | Required | Notes |
|---|---|---|
| **Name** | Yes | Used as the person's `CommonName` in Feenics. Also split into first/last name. |
| **Address** | No | Parsed into street, city, state, and zip automatically. |
| **Phone** | No | Stored as a "Mobile" phone number. |
| **Email** | No | Stored as a "Home" email address. |
| **Birthday** | Yes | Used to calculate scheduled access level dates. |

## Previewing the Import

1. Select a CSV file with **Browse**
2. Click **Preview**
3. A preview window shows each person with their calculated access levels, dates, and statuses (Scheduled / Active / Expired)
4. Review the data before importing — no API calls are made during preview

## Running the Import

1. Fill in all connection settings
2. Select a CSV file
3. Click **Start Import**
4. The log panel shows real-time progress including:
   - Authentication status
   - Access level resolution
   - Person creation/update results
   - Each scheduled access level assignment
5. The progress bar tracks overall completion
6. When finished, a summary dialog shows counts of created, updated, and assigned records

### Cancelling

Click **Cancel** at any time during import. The operation stops after the current API call completes. People and access levels already created are not rolled back.

## Log Output

The log panel at the bottom of the window shows timestamped messages for every operation. Lines prefixed with `DEBUG:` provide detailed diagnostic information useful for troubleshooting.

## Settings File

Settings are automatically saved to:

```
%AppData%\FeenicsCsvImport\settings.json
```

Contents include:
- API URL
- Instance name
- Username
- Duplicate handling preference
- All access level rules

The **password is never written to disk**.
