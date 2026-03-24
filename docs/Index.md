# Feenics CSV Import

Welcome to the Feenics CSV Import wiki. This tool suite includes a bulk CSV importer for the Feenics (Acre Security) cloud access control platform and a badge listener for front-desk check-in workflows.

## Pages

- [User Guide](User-Guide.md) — step-by-step instructions for the CSV import application
- [Badge Listener](Badge-Listener.md) — system-tray badge scanner for desk login check-ins
- [CSV Specification](CSV-Specification.md) — detailed format requirements for the import file
- [Access Level Rules](Access-Level-Rules.md) — how age-based scheduling works
- [Troubleshooting](Troubleshooting.md) — common errors and how to resolve them

## Quick Start — CSV Import

1. Launch `FeenicsCsvImport.Gui.exe`
2. Enter your Feenics API credentials (API URL, Instance, Username, Password)
3. Configure access level rules or use the defaults
4. Click **Export Template** to get a blank CSV, fill it in with your member data
5. Click **Browse** to select the CSV file
6. Click **Preview** to review calculated dates
7. Click **Start Import**

## Quick Start — Badge Listener

1. Launch `FeenicsCardSwipeMonitor.exe`
2. Right-click the Shield icon in the system tray → **Settings**
3. Enter your Feenics Instance, Username, Password, and COM port
4. Click **Test Connection** to verify
5. Click **Save & Encrypt**
6. Scan a badge — a DESK LOGIN note is posted to the cardholder's profile

## Project Links

- [GitHub Repository](https://github.com/siebe41/FeenicsCsvImport)
- [Feenics / Acre Security](https://www.acresecurity.com/)
