# Feenics CSV Import

A Windows desktop application for bulk-importing people from CSV files into the [Feenics (Acre Security)](https://www.acresecurity.com/) cloud access control platform, with automatic age-based scheduled access level assignment.

## Overview

This tool is designed for organizations (such as recreational associations) that need to onboard members from a spreadsheet and automatically assign scheduled access levels based on each person's age. For example, a pool and gym facility can grant:

- **Pool-only access** from age 12 to 14
- **Pool and gym access** from age 14 to 18
- **Full after-hours access** from age 18 onward (permanent)

The application connects to the Feenics Keep API, creates person records, and assigns scheduled access levels with date ranges calculated from each person's date of birth.

## Features

- **WPF GUI** with connection settings, rule editor, CSV preview, progress bar, and detailed log output
- **CSV template export** — generate a blank CSV with the correct headers and sample data
- **Configurable access level rules** — define any number of rules mapping age ranges to Feenics access levels
- **Load access levels from instance** — fetch existing access levels from your Feenics instance and add them as rules
- **Auto-create access levels** — optionally create access levels in Feenics if they don't already exist
- **Duplicate handling** — choose how to handle people who already exist in Feenics:
  - **Skip** — leave existing people untouched (default)
  - **Update** — overwrite the existing person's data with CSV values
  - **Create new** — always create a new record (with duplicate warning)
- **Paginated people lookup** — correctly detects existing people even in large instances
- **Preview window** — review calculated access dates and statuses before importing
- **Rate-limit retry** — automatic exponential backoff with jitter on HTTP 429 responses
- **Persistent settings** — connection details, rules, and preferences are saved to `%AppData%\FeenicsCsvImport\settings.json` (password is never saved)
- **Cancellation support** — cancel a running import at any time

## Solution Structure

| Project | Description |
|---|---|
| **FeenicsCsvImport.Gui** | WPF desktop application (main entry point) |
| **FeenicsCsvImport.ClassLibrary** | Core logic — CSV parsing, API interaction, access level scheduling |
| **FeenicsCsvImport** | Console application for headless/scripted imports |
| **FeenicsCsvImport.Test** | Unit tests (MSTest) |

## Requirements

- **.NET Framework 4.8**
- Windows (WPF)
- A Feenics Keep cloud instance with valid credentials

## CSV Format

The CSV file must include the following columns:

| Column | Description | Example |
|---|---|---|
| `Name` | Full name (first and last) | `John Smith` |
| `Address` | Street address (single line) | `123 Main St, Springfield, IL 62701` |
| `Phone` | Phone number | `555-123-4567` |
| `Email` | Email address | `john.smith@example.com` |
| `Birthday` | Date of birth | `03/15/2010` |

Use the **Export Template** button in the application to generate a correctly formatted CSV file with sample rows.

## Access Level Rules

Rules are defined in the application's rule grid and determine which Feenics access levels are assigned to each person based on their age:

| Field | Description |
|---|---|
| **Access Level Name** | Must match an existing Feenics access level name (or check **Create** to auto-create it) |
| **Start Age** | Age (in years) when this access level becomes active |
| **End Age** | Age when this access level expires — leave blank for permanent access |
| **Create** | If checked, the access level will be created in Feenics if it doesn't already exist |

### Default Rules

| Access Level Name | Start Age | End Age | Notes |
|---|---|---|---|
| `PoolOnlyAccess-Age12` | 12 | 14 | Pool only for ages 12–14 |
| `PoolAndGymAccess-Age14` | 14 | 18 | Pool and gym for ages 14–18 |
| `PoolAndGymAfterHoursAccess-Age18` | 18 | *(blank)* | Full access from age 18, permanent |

### How Scheduling Works

For each person, every rule generates a scheduled access level assignment:

- **ActiveOn** = person's date of birth + Start Age
- **ExpiresOn** = person's date of birth + End Age, or 50 years from today if End Age is blank

Rules where the expiration date has already passed are automatically skipped.

## Getting Started

1. **Build** the solution in Visual Studio (requires .NET Framework 4.8 targeting pack)
2. **Run** `FeenicsCsvImport.Gui`
3. Enter your **API URL**, **Instance**, **Username**, and **Password**
4. Configure **access level rules** (or use the defaults, or click **Load from Instance** to fetch existing ones)
5. Click **Export Template** to get a blank CSV, then fill it in with your member data
6. Click **Browse** to select your completed CSV file
7. Click **Preview** to verify the calculated access dates look correct
8. Click **Start Import**

## Settings Persistence

Settings are saved to:

```
%AppData%\FeenicsCsvImport\settings.json
```

This includes API URL, instance name, username, duplicate handling preference, and all access level rules. **The password is never saved.**

## Import Process

1. **Authenticate** with the Feenics API
2. **Resolve access levels** — match rule names to existing access levels (or create missing ones)
3. **Read CSV** and parse all records
4. **Check for duplicates** — paginate through all existing people in the instance
5. **Create or update people** based on the selected duplicate handling mode
6. **Assign scheduled access levels** — for each person and each rule, calculate the date range and call the API
7. **Report results** — summary of created, updated, assigned, skipped, and failed records

## Running Tests

The test project uses MSTest. Run tests from Visual Studio Test Explorer or via:

```
vstest.console.exe FeenicsCsvImport.Test\bin\Debug\FeenicsCsvImport.Test.dll