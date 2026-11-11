; ==============================================================================
; NEXUS-3D Professional Windows Installer Configuration (Inno Setup 6)
; Product: NEXUS-3D – Universal 3D Model Converter & Editor
; Version: 1.0.0
; Architecture: Windows 64-bit (x64)
; ==============================================================================

#define MyAppName "NEXUS-3D"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ManhCuong-Code"
#define MyAppURL "https://github.com/ManhCuong-Code/NEXUS-3D"
#define MyAppExeName "NEXUS3D.App.exe"
#define MyCliExeName "nexus3d.exe"

[Setup]
AppId={{5A8D2C3B-7E1F-4B89-9A2E-3D4F5C6E7A8B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\dist\installer
OutputBaseFilename=NEXUS-3D-v{#MyAppVersion}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=lowest
DisableProgramGroupPage=auto
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full Installation (WPF Studio, CLI, Sample Plugins & SDK)"
Name: "compact"; Description: "Compact Installation (WPF Studio Only)"
Name: "cli"; Description: "Headless CLI Only"
Name: "custom"; Description: "Custom Installation"; Flags: iscustom

[Components]
Name: "app"; Description: "NEXUS-3D Desktop GUI Studio"; Types: full compact custom; Flags: fixed
Name: "cli"; Description: "NEXUS-3D Headless CLI (nexus3d.exe)"; Types: full cli custom
Name: "plugins"; Description: "Bundled Plugins (RawVox 3D format extension)"; Types: full custom
Name: "sdk"; Description: "Plugin SDK & Developer Documentation"; Types: full custom

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "envPath"; Description: "Add NEXUS-3D CLI to user PATH environment variable"; GroupDescription: "Command Line Options:"; Flags: unchecked; Components: cli

[Files]
; WPF Desktop Application Files
Source: "..\dist\publish\wpf\*"; DestDir: "{app}\Application"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: app

; Standalone CLI Files
Source: "..\dist\publish\cli\*"; DestDir: "{app}\CLI"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: cli

; Bundled Plugins
Source: "..\plugins\NEXUS3D.Sample.Plugin\bin\Release\net10.0\*"; DestDir: "{app}\Plugins\NEXUS3D.Sample.Plugin"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: plugins
Source: "..\plugins\NEXUS3D.Sample.Plugin\manifest.json"; DestDir: "{app}\Plugins\NEXUS3D.Sample.Plugin"; Flags: ignoreversion; Components: plugins

; Documentation and SDK
Source: "..\README.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion; Components: sdk
Source: "..\RELEASE_NOTES.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion
Source: "..\documentation\formats\plugin-sdk-architecture.md"; DestDir: "{app}\Documentation"; Flags: ignoreversion; Components: sdk
Source: "..\dist\nuget\*.nupkg"; DestDir: "{app}\SDK"; Flags: ignoreversion skipifsourcedoesntexist; Components: sdk

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\Application\{#MyAppExeName}"; Components: app
Name: "{group}\{#MyAppName} CLI"; Filename: "{app}\CLI\{#MyCliExeName}"; Components: cli
Name: "{group}\Documentation"; Filename: "{app}\Documentation\README.md"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Application\{#MyAppExeName}"; WorkingDir: "{app}\Application"; IconFilename: "{app}\Application\Resources\app.ico"; Tasks: desktopicon; Components: app

[Registry]
; File Associations (Optional inspection in NEXUS-3D)
Root: HKCU; Subkey: "Software\Classes\.schem\OpenWithProgids"; ValueType: string; ValueName: "NEXUS3D.Model"; ValueData: ""; Flags: uninsdeletevalue; Components: app
Root: HKCU; Subkey: "Software\Classes\.schematic\OpenWithProgids"; ValueType: string; ValueName: "NEXUS3D.Model"; ValueData: ""; Flags: uninsdeletevalue; Components: app
Root: HKCU; Subkey: "Software\Classes\.rawvox\OpenWithProgids"; ValueType: string; ValueName: "NEXUS3D.Model"; ValueData: ""; Flags: uninsdeletevalue; Components: app
Root: HKCU; Subkey: "Software\Classes\NEXUS3D.Model"; ValueType: string; ValueName: ""; ValueData: "3D & Schematic Model Document"; Flags: uninsdeletekey; Components: app
Root: HKCU; Subkey: "Software\Classes\NEXUS3D.Model\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Application\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Components: app

; Add CLI to User PATH environment variable
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}\CLI"; Tasks: envPath; Check: NeedsAddPath(ExpandConstant('{app}\CLI'))

[Run]
Filename: "{app}\Application\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent; Components: app

[Code]
// Helper function to check if directory is already in user PATH
function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath) then
  begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + UpperCase(Param) + ';', ';' + UpperCase(OrigPath) + ';') = 0;
end;
