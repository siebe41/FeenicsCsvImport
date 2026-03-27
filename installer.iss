[Setup]
AppName=Feenics Tools
; Read the version from the command line variable
AppVersion={#MyAppVersion}
; Force installation into the All Users Program Files directory
DefaultDirName={commonpf}\Feenics Tools
DefaultGroupName=Feenics Tools
OutputDir=Output
; Append the version to the installer filename
OutputBaseFilename=FeenicsToolsSetup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
; Force Admin rights (Required for All Users install)
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

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcuts"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "startup_monitor"; Description: "Automatically start Card Swipe Monitor on Windows login"; GroupDescription: "Startup options:"; Components: monitor; Flags: unchecked
Name: "startup_import"; Description: "Automatically start CSV Import Tool on Windows login"; GroupDescription: "Startup options:"; Components: import; Flags: unchecked

[Icons]
Name: "{group}\Card Swipe Monitor"; Filename: "{app}\Monitor\FeenicsCardSwipeMonitor.exe"; Components: monitor
; Force shortcut to the Public/All Users Desktop
Name: "{commondesktop}\Card Swipe Monitor"; Filename: "{app}\Monitor\FeenicsCardSwipeMonitor.exe"; Tasks: desktopicon; Components: monitor

Name: "{group}\CSV Import Tool"; Filename: "{app}\ImportTool\FeenicsCsvImport.Gui.exe"; Components: import
; Force shortcut to the Public/All Users Desktop
Name: "{commondesktop}\CSV Import Tool"; Filename: "{app}\ImportTool\FeenicsCsvImport.Gui.exe"; Tasks: desktopicon; Components: import

; Force shortcuts to the All Users Startup folder
Name: "{commonstartup}\Card Swipe Monitor"; Filename: "{app}\Monitor\FeenicsCardSwipeMonitor.exe"; Tasks: startup_monitor; Components: monitor
Name: "{commonstartup}\CSV Import Tool"; Filename: "{app}\ImportTool\FeenicsCsvImport.Gui.exe"; Tasks: startup_import; Components: import

[Run]
Filename: "{app}\Monitor\FeenicsCardSwipeMonitor.exe"; Description: "Launch Card Swipe Monitor"; Flags: nowait postinstall skipifsilent; Components: monitor