; Instalador do TempoForge (Inno Setup 6).
; Gere antes a pasta publish\ com tools\build-release.ps1, que também chama este script.

#define AppName "TempoForge"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#define AppExe "TempoForge.exe"

[Setup]
AppId={{6C0B3A52-9D4E-4F7A-B1C8-5E2A7D90F314}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=TempoForge
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE
SetupIconFile=..\src\TempoForge.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=Output
OutputBaseFilename=TempoForge-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; Atualização automática: o app chama o instalador com /SILENT /RELAUNCH=1 e é reaberto no fim.
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: IsRelaunch

[Code]
function IsRelaunch: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;
