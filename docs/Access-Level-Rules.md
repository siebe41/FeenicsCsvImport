# Access Level Rules

## Overview

Access level rules map a person's age to Feenics access levels. Each rule defines:

- A **Feenics access level name** to assign
- A **Start Age** — the age at which access activates
- An **End Age** — the age at which access expires (blank = permanent)

During import, every rule is evaluated for every person. The person's date of birth is used to calculate concrete dates.

## Date Calculation

| Field | Formula |
|---|---|
| **ActiveOn** | Date of birth + Start Age (in years) |
| **ExpiresOn** | Date of birth + End Age (in years), or **50 years from today** if End Age is blank |

### Example

For a person born on `2012-06-15` with the default rules:

| Rule | Start Age | End Age | ActiveOn | ExpiresOn |
|---|---|---|---|---|
| PoolOnlyAccess-Age12 | 12 | 14 | 2024-06-15 | 2026-06-15 |
| PoolAndGymAccess-Age14 | 14 | 18 | 2026-06-15 | 2030-06-15 |
| PoolAndGymAfterHoursAccess-Age18 | 18 | *(blank)* | 2030-06-15 | 2075-*today* |

## Automatic Skip for Expired Rules

If a rule's calculated ExpiresOn date is in the past for a given person, it is **automatically skipped** and not sent to the API. This means an adult being imported won't get a "pool only ages 12–14" assignment that already expired years ago.

## End Age of 0 or Blank

Both `null` (blank) and `0` are treated the same — the rule is considered **permanent** (no expiry). The display shows `18+` instead of `18-0`.

When sent to the Feenics API, a concrete ExpiresOn date is required. Permanent rules use **50 years from the current date** as the expiration.

## Create If Missing

If the **Create** checkbox is checked for a rule and the named access level doesn't exist in the Feenics instance, the application will:

1. Fetch the instance's root folder (requires folder-view permission)
2. Create a new access level with that name
3. Use the newly created access level for assignments

If **Create** is unchecked and the access level doesn't exist, the import **stops with an error** before creating any people.

> **Note**: The root folder fetch is only performed when at least one access level needs to be created. If all access levels already exist, no folder-level permission is required.

## Loading Rules from the Instance

The **Load from Instance** button connects to the API, retrieves all existing access level names, and presents a picker dialog. Selected access levels are added to the rules grid with:

- **Start Age** = 0
- **End Age** = blank (permanent)
- **Create** = unchecked

You should then edit the Start Age and End Age for each rule to match your age-based access policy.

## Preview

The **Preview** button (available after selecting a CSV file) opens a window showing every person with their calculated access levels:

| Status | Meaning |
|---|---|
| **Scheduled** | ActiveOn is in the future |
| **Active** | ActiveOn is in the past and ExpiresOn is in the future (or no expiry) |
| **Expired** | ExpiresOn is in the past |
