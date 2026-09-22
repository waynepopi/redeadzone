; Deadzone Inno Setup installer script
; Build with: ISCC installer\Deadzone.iss
; Requires Inno Setup 7.x (https://jrsoftware.org/isinfo.php)

#define MyAppName "Deadzone"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Deadzone contributors"
#define MyAppExeName "Deadzone.exe"
#define MyPublishDir "..\artifacts\publish\win-x64"

[Setup]
AppId={{7F2A3C4D-8B1E-4F5A-9D6C-2E3F4A5B6C7D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\release
OutputBaseFilename=Deadzone-v{#MyAppVersion}-Setup
SetupIconFile=..\assets\Deadzone.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
MinVersion=10.0.17763

; No code-signing in v0.1.0 — can be added later via SignTool
; SignTool=

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "launchapp"; Description: "&Launch Deadzone after installation"; GroupDescription: "After installation:"; Flags: unchecked

[Files]
; Copy the entire self-contained publish output
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Optional: launch after install
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent; Tasks: launchapp

[UninstallRun]
; Best-effort removal of the Deadzone startup scheduled task
; Failure (task not found) is silently ignored.
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""Deadzone"" /F"; Flags: runhidden; RunOnceId: "RemoveDeadzoneTask"

[UninstallDelete]
; Remove the application directory (program files only — NOT %LOCALAPPDATA%\Deadzone)
Type: filesandordirs; Name: "{app}"

[Code]
// Close Deadzone if it is running before install/uninstall proceeds.
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CloseDeadzoneIfRunning();
var
  ResultCode: Integer;
begin
  // Gently ask Windows to close the Deadzone process before replacing files.
  // /F force-closes only the specific process image — does not affect other applications.
  Exec('taskkill.exe', '/IM Deadzone.App.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // Ignore ResultCode — process may not be running.
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CloseDeadzoneIfRunning();
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    CloseDeadzoneIfRunning();
end;
