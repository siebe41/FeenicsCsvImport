# Badge Listener

## Overview

The **FeenicsCardSwipeMonitor** is a Windows system-tray application that listens for badge scans from an rf IDEAS USB card reader and posts a **DESK LOGIN** check-in note to the cardholder's profile in Feenics.

It is designed for front-desk workflows where staff need to log member check-ins without opening the Feenics dashboard.

## How It Works

1. The application starts in the system tray (Shield icon)
2. The application connects directly to the USB reader using the **rf IDEAS pcProx API** and continuously polls for a card presence.
3. When a badge is scanned:
   - The raw 24-bit Wiegand data is pulled from the reader's microchip.
   - The data is bitwise-inverted and masked to extract the 5-digit encoded Card ID.
   - The badge number is copied to the **clipboard**.
   - If Feenics logging is enabled, the cardholder is looked up in Feenics.
   - Any existing `***DESK LOGIN***` note is **replaced** with a new one containing the current timestamp.
   - A **balloon notification** confirms the check-in.

### DESK LOGIN Note

Each scan produces a note on the person's profile in the format:

```text
***DESK LOGIN*** - 2026-03-25 14:30:22
```

Only one DESK LOGIN note exists per person at any time. Each new scan replaces the previous note, so the profile always shows the most recent check-in.

## Setup

### First Launch

1. Run `FeenicsCardSwipeMonitor.exe`
2. The Shield icon appears in the system tray
3. Right-click the icon -> **Settings**

### Settings Window

| Field | Description |
|---|---|
| **Instance Name** | Your Feenics/Acre cloud instance name (case-sensitive) |
| **API Username** | Feenics login username |
| **API Password** | Feenics login password |
| **Enable Feenics API Logging** | Toggle switch to enable or disable sending the scan to the cloud. When disabled, the reader will only copy the badge number to the clipboard. |

*(Note: Legacy COM port and Serial settings may be visible in the UI but are bypassed by the direct USB SDK connection).*

Click **Save & Encrypt** to persist settings. The password is encrypted using Windows DPAPI (`ProtectedData.Protect` with `DataProtectionScope.CurrentUser`) - it can only be decrypted by the same Windows user on the same machine.

## rf IDEAS SDK Integration & Licensing

This application integrates with rf IDEAS hardware using the **Universal SDK**. 

To comply with licensing and operational requirements:
- The application relies on the unmanaged C++ library `pcProxAPI.dll` and its associated helper dependencies.
- These libraries must be distributed alongside the executable in the application's root directory (`bin\Debug` or the published folder).
- Because `pcProxAPI.dll` is a 32-bit library, the application **must be compiled and run as an x86 (32-bit) process**. 
- The DLL location is resolved at runtime using the Windows API `SetDllDirectory` hook to ensure reliable execution across different environments.

## Testing

### Test Connection

Click the **Test Connection** button in the Settings window to verify the system:

- **API**: Authenticates with the Feenics API and displays the connected instance name.
- **Reader**: Probes the USB ports via the SDK to confirm the rf IDEAS reader is attached and responding.

### Simulate Scan

To test the full check-in flow without a physical card reader:

1. Enter a 5-digit badge number in the **Simulate Badge Scan** field
2. Click **Simulate Scan**
3. The application runs the same flow as a real scan.

> **Tip**: Use credentials from the form fields if you haven't saved yet. The simulate button reads form values first and falls back to saved settings.

## Card Lookup

When a badge is scanned, the cardholder is found using a two-step lookup:

1. **Direct lookup** - `GetPersonByActiveCardAsync` queries the Feenics API by card number (fast, single API call)
2. **Fallback search** - if the direct lookup fails, all people are paginated and their `CardAssignments` are checked for a matching `DisplayCardNumber` or `EncodedCardNumber`

The scanned value must match an **active** card assignment in the Feenics instance.

## Note Replacement Logic

Both the DESK LOGIN and Birthday notes use replacement logic:

| Note Prefix | Behavior |
|---|---|
| `***DESK LOGIN***` | Existing note is removed before adding the new one. Each person has at most one DESK LOGIN note. |
| `Birthday:` | During CSV import, existing birthday notes are removed before adding the updated one. |

This prevents note accumulation from repeated scans or re-imports.

## Tray Menu

Right-click the Shield icon in the system tray:

| Menu Item | Action |
|---|---|
| **Settings** | Opens the configuration window |
| **Exit** | Disconnects the reader, hides the tray icon, and shuts down |

## Architecture

The badge listener is a WPF application with no main window (`ShutdownMode="OnExplicitShutdown"`).

| File | Purpose |
|---|---|
| `App.xaml.cs` | Entry point - sets DLL directories, creates tray icon, initializes `ImportService`, connects to USB reader |
| `SettingsWindow.xaml` / `.cs` | Configuration UI - credentials, logging toggle, test connection, simulate scan |
| `Properties\Settings.settings` | Persisted user settings (instance, username, encrypted password, logging preference) |

It depends on the shared **FeenicsCsvImport.ClassLibrary** project for all Feenics API interaction via `ImportService`.

## Troubleshooting

### Reader & SDK Errors

| Symptom | Cause | Fix |
|---|---|---|
| `BadImageFormatException` crash on launch | Architecture mismatch | The app is attempting to run as 64-bit. Ensure the project's Platform Target is explicitly set to **x86** in the Configuration Manager. |
| `DllNotFoundException` for `pcProxAPI.dll` | Missing dependencies | The application cannot find the SDK library or its helper files. Ensure all DLLs from the rf IDEAS installation folder were copied to the output directory. |
| Reader beeps but no scan registers | SDK Polling Failure | Ensure the reader is an SDK-compatible model (e.g., AK0 suffix) and not a keystroke emulator. Disconnect and reconnect the USB. |

### API errors

| Symptom | Cause | Fix |
|---|---|---|
| `Login failed` | Wrong credentials | Verify instance name (case-sensitive), username, and password |
| `No person found associated with card XXXXX` | Card number not in Feenics | Ensure the card is assigned as an active card assignment in the Feenics dashboard |
| `username doesn't match the regular expression` | Empty or missing credentials | Fill in credentials in the form or save them first |

### No notification after scan

- Notifications only appear for messages containing "DESK LOGIN"
- Check Windows Focus Assist / Do Not Disturb settings
- Verify the card number matches an active assignment in Feenics