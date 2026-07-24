; Inno Setup Script for Limbus Split Pro
#define MyAppName "Limbus Split Pro"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Limbus Audio Systems"
#define MyAppURL "https://github.com/joel/LimbusSplitPro"
#define MyAppExeName "LimbusSplitPro.exe"

[Setup]
AppId={{8F92D0A4-3E77-4C91-B892-D029A887E412}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\legal\THIRD_PARTY_NOTICES.txt
OutputDir=..\dist
OutputBaseFilename=LimbusSplitPro_v1.0.0_Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequiredOverridesAllowed=dialog commandline
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\src\LimbusSplitPro.App\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\legal\THIRD_PARTY_NOTICES.txt"; DestDir: "{app}\legal"; Flags: ignoreversion
Source: "..\legal\LGPL_COMPLIANCE.md"; DestDir: "{app}\legal"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Limbus Split Pro\Cache"
