---
layout: default
title: Troubleshooting
nav_order: 6
---

# Troubleshooting

## Common Errors

### "The process cannot access the file ... because it is being used by another process"

**Cause**: The CSV file is open in another program (usually Excel).

**Fix**: Close the file in Excel (or any other program) before clicking Preview or Start Import.

---

### "Missing permission to view Folder"

**Cause**: The Feenics API user account doesn't have permission to view the instance's root folder. This call is only made when an access level needs to be **created** (the "Create" checkbox is checked).

**Fix**:
- If you don't need to create access levels, make sure all access level names in your rules already exist in the Feenics instance and uncheck all **Create** checkboxes.
- If you do need to create access levels, contact your Feenics administrator to grant folder-view permission to your user account.

---

### "Login failed"

**Cause**: Incorrect API URL, instance name, username, or password.

**Fix**:
- Verify the API URL (default: `https://api.us.acresecurity.cloud`)
- Check the instance name — this is case-sensitive
- Check the instance name — this is case-sensitive
- Re-enter the password (it is not saved between sessions)

---

### "Access level not found: 'SomeName'. Enable 'Create' to auto-create it."

**Cause**: A rule references an access level name that doesn't exist in the Feenics instance, and the **Create** checkbox is unchecked.

**Fix**: Either:
- Check the **Create** checkbox for that rule, or
- Correct the access level name to match an existing one (use **Load from Instance** to see what's available), or
- Create the access level manually in Feenics first

---

### "429 Too Many Requests" (rate limiting)

**Cause**: The Feenics API is rate-limiting your requests. This happens during large imports.

**Fix**: The application handles this automatically with exponential backoff retry (up to 5 retries). If it still fails:
- Reduce the import batch size by splitting the CSV into smaller files
- Try again later when API load is lower

---

### "Could not find created person: SomeName"

**Cause**: A person was created in the batch but couldn't be found when querying back for access level assignment. This can happen if the API has propagation delay.

**Fix**: This is logged as a warning. The person was likely created successfully — run the import again with **Skip** mode to assign access levels to anyone who was missed.

---

### ExpiresOn is the same date as ActiveOn

**Cause**: The End Age was set to `0` instead of being left blank. In earlier versions this caused both dates to be the DOB.

**Fix**: This was fixed — End Age of `0` is now treated as blank (permanent access). Clear the End Age cell or leave it empty.

---

## Diagnostic Information

### Log Output

The log panel shows detailed timestamped output. Lines prefixed with `DEBUG:` include:
- API URLs and instance keys
- Existing access levels found
- Rule-to-access-level matching
- Per-person date calculations
- API call parameters and responses

### Settings File

Saved at `%AppData%\FeenicsCsvImport\settings.json`. If the application fails to start or loads unexpected settings, you can:
1. Close the application
2. Delete or rename the settings file
3. Relaunch — default settings will be regenerated
3. Relaunch — default settings will be regenerated

### Reporting Issues

When reporting a bug, include:
1. The full log output (copy from the log panel)
2. The settings file content (with credentials removed)
3. A sanitized sample of the CSV file (with real names/addresses removed)
