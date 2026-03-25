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
ArchitecturesInstallIn64BitMode=x64

[Types]
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "monitor"; Description: "Feenics Card Swipe Monitor (System Tray App)"; Types: custom
Name: "import"; Description: "Feenics CSV Import Tool"; Types: custom

[Files]
Source: "FeenicsCardSwipeMonitor\bin\x64\Release\FeenicsCardSwipeMonitor.exe"; DestDir: "{app}\Monitor"; Components: monitor; Flags: ignoreversion
Source: "FeenicsCardSwipeMonitor\bin\x64\Release\*"; DestDir: "{app}\Monitor"; Components: monitor; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "FeenicsCsvImport.Gui\bin\x64\Release\FeenicsCsvImport.Gui.exe"; DestDir: "{app}\ImportTool"; Components: import; Flags: ignoreversion
Source: "FeenicsCsvImport.Gui\bin\x64\Release\*"; DestDir: "{app}\ImportTool"; Components: import; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Card Swipe Monitor"; Filename: "{app}\Monitor\FeenicsCardSwipeMonitor.exe"; Components: monitor
Name: "{autodesktop}\Card Swipe Monitor"; Filename: "{app}\Monitor\FeenicsCardSwipeMonitor.exe"; Tasks: desktopicon; Components: monitor
Name: "{group}\CSV Import Tool"; Filename: "{app}\ImportTool\FeenicsCsvImport.Gui.exe"; Components: import
Name: "{autodesktop}\CSV Import Tool"; Filename: "{app}\ImportTool\FeenicsCsvImport.Gui.exe"; Tasks: desktopicon; Components: import

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcuts"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\Monitor\FeenicsCardSwipeMonitor.exe"; Description: "Launch Card Swipe Monitor"; Flags: nowait postinstall skipifsilent; Components: monitor
