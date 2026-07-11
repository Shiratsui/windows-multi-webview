#define AppName "Multi WebView"
#define AppExeName "MultiWebView.exe"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\MultiWebView\bin\Release\net10.0-windows\win-x64\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{9D2FC891-9E8C-4F2E-A6FC-39D3F6B265B7}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Multi WebView
DefaultDirName={localappdata}\Programs\Multi WebView
DefaultGroupName=Multi WebView
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=MultiWebViewSetup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
SetupIconFile=..\MultiWebView\icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Multi WebView"; Filename: "{app}\{#AppExeName}"
Name: "{userdesktop}\Multi WebView"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,Multi WebView}"; Flags: nowait postinstall skipifsilent

[Code]
function IsWebView2RuntimeInstalled(): Boolean;
var
  Version: String;
begin
  Result :=
    RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
    RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
    RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not IsWebView2RuntimeInstalled() then
    begin
      MsgBox(
        'Multi WebView requires the Microsoft Edge WebView2 Runtime.' + #13#10 + #13#10 +
        'If the app does not start on this machine, install the WebView2 Runtime from Microsoft.',
        mbInformation,
        MB_OK);
    end;
  end;
end;
