[Setup]
AppName=Feenics Tools
; Read the version from the command line variable
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\Feenics Tools
DefaultGroupName=Feenics Tools
OutputDir=Output
; Append the version to the installer filename
OutputBaseFilename=FeenicsToolsSetup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

[Types]
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "monitor"; Description: "Feenics Card Swipe Monitor (System Tray App)"; Types: custom
Name: "import"; Description: "Feenics CSV Import Tool"; Types: custom

[Files]
; Monitor App - Pointing to the MSBuild Release folder
Source: "FeenicsCardSwipeMonitor\bin\Release\FeenicsCardSwipeMonitor.exe"; DestDir: "{app}\Monitor"; Components: monitor; Flags: ignoreversion
Source: "FeenicsCardSwipeMonitor\bin\Release\*"; DestDir: "{app}\Monitor"; Components: monitor; Flags: ignoreversion recursesubdirs createallsubdirs

; Import Tool - Pointing to the MSBuild Release folder (Update folder name if different)
Source: "FeenicsCsvImport\bin\Release\FeenicsCsvImport.exe"; DestDir: "{app}\ImportTool"; Components: import; Flags: ignoreversion
Source: "FeenicsCsvImport\bin\Release\*"; DestDir: "{app}\ImportTool"; Components: import; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Card Swipe Monitor"; Filename: "{app}\Monitor\FeenicsCardSwipeMonitor.exe"; Components: monitor
Name: "{group}\CSV Import Tool"; Filename: "{app}\ImportTool\FeenicsCsvImport.exe"; Components: import
Name: "{autodesktop}\Card Swipe Monitor"; Filename: "{app}\Monitor\FeenicsCardSwipeMonitor.exe"; Tasks: desktopicon; Components: monitor
Name: "{autodesktop}\CSV Import Tool"; Filename: "{app}\ImportTool\FeenicsCsvImport.exe"; Tasks: desktopicon; Components: import

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcuts"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\Monitor\FeenicsCardSwipeMonitor.exe"; Description: "Launch Card Swipe Monitor"; Flags: nowait postinstall skipifsilent; Components: monitor
