; WorkLogManager Windows installer script (Inno Setup)
;
; Builds a native Windows installer that:
;   1. Copies the self-contained published API (already including wwwroot/ with the
;      built front-end) to {app}.
;   2. Registers the API as a Windows Service ("WorkLogManagerApi"), running under
;      LocalSystem, set to start automatically.
;   3. Starts the service. On first run, the application itself creates the SQLite
;      database file and applies pending migrations (see Database.Migrate() in
;      Program.cs) — no separate database provisioning step is needed here.
;   4. On upgrade (installer run again over an existing installation), stops the
;      service before overwriting the binaries and restarts it afterwards. The SQLite
;      database file lives under {commonappdata}\WorkLogManager, outside {app}, so the
;      [Files] copy step never touches it — upgrades preserve existing data.
;   5. On uninstall, removes everything: the installation directory ({app}) and the
;      ProgramData folder ({commonappdata}\WorkLogManager, including the .db file).
;      This is a deliberate decision (see plano-desenvolvimento.md, section 12,
;      "Alterações pós-aprovação v2.1") — no residue is left on the machine.
;
; NOTE: this script cannot be compiled/tested in this development environment
; (macOS/Linux, no Inno Setup available). It was written and manually reviewed against
; the Inno Setup documented syntax, but must be compiled and validated end-to-end by a
; developer on a Windows machine with Inno Setup installed (see installer/README.md).

#define MyAppName "WorkLogManager"
#define MyAppExeName "WorkLogManager.Api.exe"
#define MyServiceName "WorkLogManagerApi"
; Read from environment variable so build.ps1 can pass the version coming from the
; publish step without editing this file for every release.
#define MyAppVersion GetEnv("WORKLOGMANAGER_VERSION")
#if MyAppVersion == ""
  #define MyAppVersion "0.0.0"
#endif

[Setup]
; Fixed AppId (generated once) — required for Inno Setup to detect upgrades vs. fresh
; installs across versions. Never change this value.
AppId={{DE3D0835-52AB-4C88-94CA-7FBC313F5D74}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Registering a Windows Service requires administrative privileges.
PrivilegesRequired=admin
OutputDir=output
OutputBaseFilename=WorkLogManagerSetup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Dirs]
; Machine-wide data directory for the SQLite database file, used because the service
; runs under LocalSystem (no per-user profile). Created if it doesn't exist yet; never
; touched by [Files] on upgrade.
Name: "{commonappdata}\{#MyAppName}"

[Files]
; Publish output of `dotnet publish -c Release -r win-x64 --self-contained`, already
; including wwwroot/ with the front-end production build (see build.ps1).
Source: "publish\api\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Static appsettings.Production.json, already pointing at the fixed ProgramData path —
; no secrets, no dynamic substitution needed (SQLite connection strings have no
; credentials). Copied after the publish output so it always wins over anything with
; the same name that might come from the publish folder.
Source: "appsettings.Production.json"; DestDir: "{app}"; Flags: ignoreversion

[Code]
function ServiceExists(const ServiceName: string): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('sc.exe', 'query ' + ServiceName, '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function IsUpgrade(): Boolean;
begin
  Result := ServiceExists('{#MyServiceName}');
end;

procedure StopAndRemoveExistingService();
var
  ResultCode: Integer;
begin
  if IsUpgrade() then
  begin
    Exec('sc.exe', 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1000);
    Exec('sc.exe', 'delete {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1000);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Stop (and remove) any previously registered service before [Files] overwrites the
  // executable, so Windows doesn't keep a file lock on it during an upgrade.
  if CurStep = ssInstall then
    StopAndRemoveExistingService();
end;

[Icons]
; URL shortcuts (not regular .lnk-to-exe icons): Inno Setup creates a valid
; Windows shortcut file whose target is a URL when Filename is an http(s)
; address instead of a file path. Double-clicking it opens the address in the
; user's default browser — no .url file needs to be authored separately, and
; no WorkingDir is applicable for a URL target. This lets the end user open
; WorkLogManager without ever having to know or type an address.
; IconFilename points at the installed executable purely for a recognizable
; icon in Explorer/Start Menu; it has no effect on the shortcut's behavior.
Name: "{autodesktop}\{#MyAppName}"; Filename: "http://localhost:5000"; \
  IconFilename: "{app}\{#MyAppExeName}"; Comment: "Open {#MyAppName}"
Name: "{autoprograms}\{#MyAppName}"; Filename: "http://localhost:5000"; \
  IconFilename: "{app}\{#MyAppExeName}"; Comment: "Open {#MyAppName}"

[Run]
; Note on quoting: Inno Setup Pascal-style strings use a doubled quote ("") to embed a
; literal double-quote character; backslash has no special meaning. The resulting
; command line passed to sc.exe is:
;   create WorkLogManagerApi binPath= "C:\...\WorkLogManager.Api.exe" start= auto DisplayName= "WorkLogManager"
Filename: "{sys}\sc.exe"; Parameters: "create {#MyServiceName} binPath= ""{app}\{#MyAppExeName}"" start= auto DisplayName= ""{#MyAppName}"""; \
  Flags: runhidden; StatusMsg: "Registering Windows Service..."

; Sets the service's environment variables as a REG_MULTI_SZ value named
; "Environment" directly on the service's own registry key — this is the exact
; location the Service Control Manager reads at service start to populate the
; process environment (see Microsoft docs on "Environment" service registry
; entries). The previous version of this line pointed at a "...\Environment"
; *subkey* and set its unnamed default value instead, which the SCM does not
; read at all — ASPNETCORE_ENVIRONMENT was silently never applied to the
; service process. Fixed here as part of adding ASPNETCORE_URLS, since without
; this fix the explicit port below would not take effect either.
;
; Both variables are written in a single reg.exe call for atomicity, using the
; /s "|" flag to let reg.exe split /d into multiple REG_MULTI_SZ strings on the
; "|" separator (documented reg.exe behavior; default separator is "\0", which
; cannot be typed on an Inno Setup [Run] command line, so "|" is used instead).
;
; ASPNETCORE_URLS is set explicitly to http://localhost:5000 so the published
; app never relies on Kestrel's implicit default (also localhost:5000, but
; undocumented/unversioned behavior) — see resumo-implementacao.md.
Filename: "{sys}\reg.exe"; Parameters: "add ""HKLM\SYSTEM\CurrentControlSet\Services\{#MyServiceName}"" /v Environment /t REG_MULTI_SZ /s ""|"" /d ""ASPNETCORE_ENVIRONMENT=Production|ASPNETCORE_URLS=http://localhost:5000"" /f"; \
  Flags: runhidden; StatusMsg: "Configuring environment..."

Filename: "{sys}\net.exe"; Parameters: "start {#MyServiceName}"; Flags: runhidden; StatusMsg: "Starting WorkLogManager..."

[UninstallRun]
Filename: "{sys}\net.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; RunOnceId: "StopService"
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden; RunOnceId: "DeleteService"

[UninstallDelete]
; Decision (v2.1, approved): the uninstaller removes EVERYTHING — the installation
; directory AND the ProgramData folder (including worklogmanager.db) — leaving no
; residue on the machine. Earlier drafts of this plan preserved the database on
; uninstall; that behavior was explicitly overridden by the user for this approval.
;
; The [Icons] shortcuts (desktop and Start Menu) need no entry here: Inno Setup's
; uninstaller automatically deletes every shortcut it created from [Icons] — this
; applies equally to regular file shortcuts and to URL shortcuts, since both are
; just .lnk files tracked in the same uninstall log.
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{commonappdata}\{#MyAppName}"
