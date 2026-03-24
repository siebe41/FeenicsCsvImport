---
layout: default
title: CSV Specification
nav_order: 4
---

# CSV Specification

## File Format

- **Encoding**: UTF-8 (with or without BOM)
- **Delimiter**: Comma (`,`)
- **Header row**: Required — must be the first row
- **Quoting**: Values containing commas must be enclosed in double quotes

## Required Columns

| Column | Type | Required | Description |
|---|---|---|---|
| `Name` | string | **Yes** | Person's full name. Split on the first space into first name / last name. Used as `CommonName` for duplicate detection. |
| `Address` | string | No | Single-line street address. Automatically parsed into street, city, state/province, and postal code. |
| `Phone` | string | No | Phone number (any format). Stored as type "Mobile". |
| `Email` | string | No | Email address. Stored as type "Home". |
| `Birthday` | date | **Yes** | Date of birth. Used to calculate all scheduled access level date ranges. |

## Date Formats

The `Birthday` column is parsed using `CultureInfo.InvariantCulture`. Common accepted formats:

- `MM/dd/yyyy` — `03/15/2010`
- `yyyy-MM-dd` — `2010-03-15`
- `M/d/yyyy` — `3/15/2010`

## Address Parsing

The `Address` column is parsed from right to left:

1. **Postal code** — 5-digit US zip (`62701`), zip+4 (`62701-1234`), or Canadian postal code (`M5V 2T6`)
2. **State/Province** — 2-letter abbreviation (`IL`, `ON`)
3. **City** — text after the last remaining comma
4. **Street** — everything before the city

### Examples

| Input | Street | City | State | Zip |
|---|---|---|---|---|
| `123 Main St, Springfield, IL 62701` | 123 Main St | Springfield | IL | 62701 |
| `456 Oak Ave, Apt 4B, Chicago, IL 60601-1234` | 456 Oak Ave, Apt 4B | Chicago | IL | 60601-1234 |
| `789 Maple Road, Toronto, ON M5V 2T6` | 789 Maple Road | Toronto | ON | M5V 2T6 |
| `123 Main Street` | 123 Main Street | *(empty)* | *(empty)* | *(empty)* |

## Example File

```csv
Name,Address,Phone,Email,Birthday
John Smith,"123 Main St, Springfield, IL 62701",555-123-4567,john.smith@example.com,03/15/2010
Jane Doe,"456 Oak Ave, Columbus, OH 43215",555-987-6543,jane.doe@example.com,07/22/2008
Bob Johnson,"789 Elm Blvd, Denver, CO 80202",555-456-7890,bob.j@example.com,11/30/2015
```

## Tips

- Close the CSV file in Excel before importing — Excel locks files and the import will fail with a "file in use" error
- Ensure names are consistent — duplicate detection matches on the exact `Name` value
- Empty rows at the end of the file are ignored
- Extra columns beyond the five defined columns are ignored
