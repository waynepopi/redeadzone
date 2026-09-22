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
AppPublisherURL=https://github.com/waynepopi/redeadzone
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\release
OutputBaseFilename=Deadzone-v{#MyAppVersion}-Setup
SetupIconFile=..\assets\Deadzone.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
LicenseFile=..\DISCLAIMER.txt
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
Name: "installhidhide"; Description: "Install HidHide driver (recommended to prevent double-controller input in games)"; GroupDescription: "Recommended components:"; Check: not IsHidHideInstalled
Name: "launchapp"; Description: "&Launch Deadzone after installation"; GroupDescription: "After installation:"; Flags: unchecked

[Files]
; Copy the entire self-contained publish output
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Bundled official signed HidHide installer (extracted to temp, deleted after install)
Source: "..\redist\HidHide_1.5.230_x64.exe"; DestDir: "{tmp}"; Flags: ignoreversion deleteafterinstall; Check: not IsHidHideInstalled

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Install HidHide driver if selected and not already installed
Filename: "{tmp}\HidHide_1.5.230_x64.exe"; Parameters: "/passive /norestart"; StatusMsg: "Installing HidHide controller cloaking driver..."; Tasks: installhidhide; Check: not IsHidHideInstalled; Flags: waituntilterminated

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
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

// Checks whether the HidHide driver service is installed on the system
function IsHidHideInstalled(): Boolean;
begin
  Result := RegKeyExists(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\HidHide');
end;

procedure CloseDeadzoneIfRunning();
var
  ResultCode: Integer;
begin
  // Ask Windows to close the Deadzone process before replacing files.
  Exec('taskkill.exe', '/IM Deadzone.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/IM Deadzone.App.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Automatically registers Deadzone.exe in HidHide's Whitelist registry entry
procedure AddToHidHideWhitelist();
var
  AppExe: String;
  CurrentValues: String;
begin
  if not RegKeyExists(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\HidHide\Parameters') then
    Exit;

  AppExe := ExpandConstant('{app}\{#MyAppExeName}');

  if RegQueryMultiStringValue(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\HidHide\Parameters', 'Whitelist', CurrentValues) then
  begin
    if Pos(Uppercase(AppExe), Uppercase(CurrentValues)) = 0 then
    begin
      CurrentValues := CurrentValues + #0 + AppExe + #0;
      RegWriteMultiStringValue(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\HidHide\Parameters', 'Whitelist', CurrentValues);
    end;
  end
  else
  begin
    RegWriteMultiStringValue(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\HidHide\Parameters', 'Whitelist', AppExe + #0);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CloseDeadzoneIfRunning();
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    AddToHidHideWhitelist();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    CloseDeadzoneIfRunning();
end;
