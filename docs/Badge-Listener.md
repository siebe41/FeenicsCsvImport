# Badge Listener

## Overview

The **FeenicsCardSwipeMonitor** is a Windows system-tray application that listens for badge scans from a serial card reader and posts a **DESK LOGIN** check-in note to the cardholder's profile in Feenics.

It is designed for front-desk workflows where staff need to log member check-ins without opening the Feenics dashboard.

## How It Works

1. The application starts in the system tray (Shield icon)
2. A serial port listener monitors the configured COM port for badge scans
3. When a badge is scanned:
   - The badge number is copied to the **clipboard**
   - The cardholder is looked up in Feenics by card number
   - Any existing `***DESK LOGIN***` note is **replaced** with a new one containing the current timestamp
   - A **balloon notification** confirms the check-in

### DESK LOGIN Note

Each scan produces a note on the person's profile in the format:

```
***DESK LOGIN*** — 2025-01-15 14:30:22
```

Only one DESK LOGIN note exists per person at any time. Each new scan replaces the previous note, so the profile always shows the most recent check-in.

## Setup

### First Launch

1. Run `FeenicsCardSwipeMonitor.exe`
2. The Shield icon appears in the system tray
3. Right-click the icon ? **Settings**

### Settings Window

| Field | Description |
|---|---|
| **Instance Name** | Your Feenics/Acre cloud instance name (case-sensitive) |
| **API Username** | Feenics login username |
| **API Password** | Feenics login password |
| **COM Port** | Serial port for the card reader (e.g., `COM3`) |

Click **Save & Encrypt** to persist settings. The password is encrypted using Windows DPAPI (`ProtectedData.Protect` with `DataProtectionScope.CurrentUser`) — it can only be decrypted by the same Windows user on the same machine.

### Finding the COM Port

1. Connect the card reader via USB
2. Open **Device Manager** ? expand **Ports (COM & LPT)**
3. Note the COM port number (e.g., `COM3`)
4. Enter it in the Settings window

## Testing

### Test Connection

Click the **Test Connection** button in the Settings window to verify both connections:

- **API**: Authenticates with the Feenics API and displays the connected instance name
- **COM**: Opens and closes the serial port to confirm it exists and is available

Results appear in the status area:

```
API: OK — connected to "OaksLanding".
COM: OK — COM3 opened successfully.
```

### Simulate Scan

To test the full check-in flow without a physical card reader:

1. Enter a badge number in the **Simulate Badge Scan** field
2. Click **Simulate Scan**
3. The application runs the same flow as a real scan:
   - Badge number is copied to the clipboard
   - Cardholder is looked up by card number
   - DESK LOGIN note is replaced
4. Progress and log messages appear in the status area

> **Tip**: Use credentials from the form fields if you haven't saved yet. The simulate button reads form values first and falls back to saved settings.

## Serial Port Configuration

The card reader is configured with these serial parameters:

| Parameter | Value |
|---|---|
| Baud rate | 9600 |
| Data bits | 8 |
| Parity | None |
| Stop bits | 1 |

These match the default settings for most USB HID badge readers operating in serial/keyboard-wedge mode.

## Card Lookup

When a badge is scanned, the cardholder is found using a two-step lookup:

1. **Direct lookup** — `GetPersonByActiveCardAsync` queries the Feenics API by card number (fast, single API call)
2. **Fallback search** — if the direct lookup fails, all people are paginated and their `CardAssignments` are checked for a matching `DisplayCardNumber` or `EncodedCardNumber`

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
| **Exit** | Closes the serial port, hides the tray icon, and shuts down |

## Architecture

The badge listener is a WPF application with no main window (`ShutdownMode="OnExplicitShutdown"`).

| File | Purpose |
|---|---|
| `App.xaml.cs` | Entry point — creates tray icon, initializes `ImportService`, opens serial port |
| `SettingsWindow.xaml` / `.cs` | Configuration UI — credentials, COM port, test connection, simulate scan |
| `Properties\Settings.settings` | Persisted user settings (instance, username, encrypted password, COM port) |

It depends on the shared **FeenicsCsvImport.ClassLibrary** project for all Feenics API interaction via `ImportService`.

## Troubleshooting

### COM port errors

| Symptom | Cause | Fix |
|---|---|---|
| `COM: FAILED — The port 'COMx' does not exist` | Wrong port number | Check Device Manager for the correct port |
| `COM: FAILED — Access to the port is denied` | Another application has the port open | Close other serial terminal apps or badge software |
| Reader works briefly then stops | USB power management | Disable USB selective suspend in Power Options |

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
