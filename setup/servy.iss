#define MyAppName "Servy"
#ifndef MyAppVersion
  #define MyAppVersion "1.0"  ; default if not provided
#endif
#define MyAppPublisher "Akram El Assas"
#define MyAppURL "https://servy-win.github.io/"
#define DocsURL "https://github.com/aelassas/servy/wiki"
#define MyAppExeName "Servy.exe"

#define ManagerAppName "Servy Manager"
#define ManagerAppExeName "Servy.Manager.exe"

#define CliExeName "servy-cli.exe"

#ifndef Arch
  #define Arch "x64"
#endif

#ifndef BuildConfiguration
  #define BuildConfiguration "Release"
#endif

#ifndef Tfm
  #define Tfm "net10.0-windows"
#endif

#define AppIdGuidX64   "8343B121-BE1C-463F-AA5B-FD237DD2F8D0"
#define AppIdGuidArm64 "8343B121-BE1C-463F-AA5B-FD237DD2F8D1"

#if Arch == "arm64"
  #define AppIdGuid    AppIdGuidArm64
  #define ArchAllowed  "arm64"
  #define Runtime      "win-arm64"
#else
  #define AppIdGuid    AppIdGuidX64
  #define ArchAllowed  "x64compatible"
  #define Runtime      "win-x64"
#endif

[Setup]
PrivilegesRequired=admin
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{{#AppIdGuid}}
SetupMutex=SetupMutex{#SetupSetting("AppId")}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DisableDirPage=no
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE.txt
OutputDir=.
OutputBaseFilename=servy-{#MyAppVersion}-{#Arch}-installer
SetupIconFile=..\src\Servy\servy.ico

Compression=lzma
LZMAAlgorithm=1
; LZMADictionarySize=65536
; LZMADictionarySize=98304
; LZMADictionarySize=131072
LZMADictionarySize=196608
LZMANumFastBytes=273
LZMAUseSeparateProcess=yes
SolidCompression=yes

ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchAllowed}
WizardStyle=modern dynamic

UsePreviousTasks=no
UsePreviousSetupType=no
AlwaysRestart=no

[Messages]
SetupAppRunningError=Setup has detected that %1 is currently running.%n%nPlease close all instances of it now, then click OK to continue, or Cancel to exit.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "install_main_app"; Description: "Install Desktop App ({#MyAppExeName})"; Types: full
Name: "install_cli"; Description: "Install CLI ({#CliExeName})"; Types: full custom
Name: "install_manager"; Description: "Install Manager App ({#ManagerAppExeName})"; Types: full custom

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "Additional Options"; Flags: checkablealone
Name: "addpath"; Description: "Add Servy to PATH"; GroupDescription: "Additional Options"; Flags: checkablealone; Components: install_cli

[Files]
; Main app EXE
Source: "..\src\Servy\bin\{#BuildConfiguration}\{#Tfm}\{#Runtime}\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion; Components: install_main_app

; appsettings.desktop.json (only copy if not present, and never uninstall)
; Source: "..\src\Servy\appsettings.desktop.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall; Components: install_main_app

; CLI
Source: "..\src\Servy.CLI\bin\{#BuildConfiguration}\{#Tfm}\{#Runtime}\publish\Servy.CLI.exe"; DestDir: "{app}"; DestName: "{#CliExeName}"; Flags: ignoreversion; Components: install_cli

; appsettings.cli.json (only copy if not present, and never uninstall)
; Source: "..\src\Servy.CLI\appsettings.cli.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall; Components: install_cli

; PowerShell Module and Dump/Restore Scripts
Source: "..\src\Servy.CLI\Servy.psm1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli
Source: "..\src\Servy.CLI\Servy.psd1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli
Source: "..\src\Servy.CLI\servy-module-examples.ps1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli
Source: "..\src\Servy.CLI\Servy-Dump.ps1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli
Source: "..\src\Servy.CLI\Servy-Restore.ps1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli

; Manager
Source: "..\src\Servy.Manager\bin\{#BuildConfiguration}\{#Tfm}\{#Runtime}\publish\{#ManagerAppExeName}"; DestDir: "{app}"; Flags: ignoreversion; Components: install_manager

; appsettings.manager.json (only copy if not present, and never uninstall)
; Source: "..\src\Servy.Manager\appsettings.manager.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall; Components: install_manager

; Scripts
Source: ".\Set-ServyExePermissions.ps1"; DestDir: "{app}"; Flags: ignoreversion;

; taskschd
; 1. Copy everything EXCEPT the config, credentials, transient state files, and test/temp scripts
Source: ".\taskschd\*"; DestDir: "{app}\taskschd"; Excludes: "smtp-config.xml, smtp-cred.xml, *.dat, *.log, temp.ps1, *.test.ps1"; Flags: ignoreversion

; 2. Preserve the config file on upgrades
Source: ".\taskschd\smtp-config.xml"; DestDir: "{app}\taskschd"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall

; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Dirs]
; Name: "{commonappdata}\Servy"; Permissions: networkservice-modify service-modify
Name: "{commonappdata}\Servy"

[Icons]
; Name: "{group}\Servy"; Filename: "{app}\Servy.exe"; IconFilename: "{app}\servy.ico"; WorkingDir: "{app}"
; Name: "{group}\Servy Manager"; Filename: "{app}\Servy.Manager.exe"; IconFilename: "{app}\servy.ico"; WorkingDir: "{app}"

Name: "{commonprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Components: install_main_app
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Components: install_main_app

Name: "{commonprograms}\{#MyAppName}\{#ManagerAppName}"; Filename: "{app}\{#ManagerAppExeName}"; Components: install_manager
Name: "{commondesktop}\{#ManagerAppName}"; Filename: "{app}\{#ManagerAppExeName}"; Tasks: desktopicon; Components: install_manager

Name: "{commonprograms}\{#MyAppName}\Uninstall"; Filename: "{uninstallexe}";

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: postinstall shellexec skipifsilent unchecked; Components: install_main_app
; Filename: "{app}\{#ManagerAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(ManagerAppName, '&', '&&')}}"; Flags: postinstall shellexec skipifsilent unchecked; Components: install_manager
Filename: "{#DocsURL}"; Description: "Open Documentation"; Flags: postinstall shellexec skipifsilent unchecked

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/im ""{#MyAppExeName}"" /t /f"; Flags: runhidden waituntilterminated; RunOnceId: StopMainApp
Filename: "{sys}\taskkill.exe"; Parameters: "/im ""{#ManagerAppExeName}"" /t /f"; Flags: runhidden waituntilterminated; RunOnceId: StopManagerApp
Filename: "{sys}\taskkill.exe"; Parameters: "/im ""{#CliExeName}"" /t /f"; Flags: runhidden waituntilterminated; RunOnceId: StopCliApp

[UninstallDelete]
; Type: filesandordirs; Name: "{app}\taskschd"

[Code]
// -----------------------------------------------------
// At least one component is required
// -----------------------------------------------------
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = wpSelectComponents then
  begin
    if not WizardIsComponentSelected('install_main_app') and
       not WizardIsComponentSelected('install_cli') and
       not WizardIsComponentSelected('install_manager') then
    begin
      MsgBox('You must select at least one component to continue.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

// -----------------------------------------------------
// Pre-Install actions:
//  - Check if a version is already installed
//  - Prepare install
// -----------------------------------------------------
function QueryUninstallRegistry(const AppIdGuidStr, ValueName: String): String;
var
  sUnInstPath, sValue, FormattedAppId: String;
begin
  sValue := '';

  // Ensure AppId is wrapped in curly braces {...}
  FormattedAppId := AppIdGuidStr;
  if (Length(FormattedAppId) > 0) and (FormattedAppId[1] <> '{') then
    FormattedAppId := '{' + FormattedAppId + '}';

  sUnInstPath := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + FormattedAppId + '_is1';

  // 1. Check 64-bit HKLM
  if not RegQueryStringValue(HKLM64, sUnInstPath, ValueName, sValue) then
  begin
    // 2. Check HKCU
    if not RegQueryStringValue(HKCU, sUnInstPath, ValueName, sValue) then
    begin
      // 3. Check 32-bit HKLM (HKLM32 automatically redirects SOFTWARE to WOW6432Node)
      RegQueryStringValue(HKLM32, sUnInstPath, ValueName, sValue);
    end;
  end;

  Result := sValue;
end;

function QueryUninstallValueAnyArch(const ValueName: String): String;
var
  AppIdGuids: array[0..1] of String;
  i: Integer;
  sResult: String;
begin
  Result := '';
  AppIdGuids[0] := '{#AppIdGuid}';
  if AppIdGuids[0] = '{#AppIdGuidArm64}' then
    AppIdGuids[1] := '{#AppIdGuidX64}'
  else
    AppIdGuids[1] := '{#AppIdGuidArm64}';

  for i := 0 to High(AppIdGuids) do
  begin
    sResult := QueryUninstallRegistry(AppIdGuids[i], ValueName);
    if sResult <> '' then
    begin
      Result := sResult;
      Exit;
    end;
  end;
end;

function GetUninstallString(): String;
begin
  Result := QueryUninstallValueAnyArch('UninstallString');
end;

function GetInstalledVersion(): String;
begin
  Result := QueryUninstallValueAnyArch('DisplayVersion');
end;

function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;

function BoolToStr(Value: Boolean): String;
begin
  if Value then
  begin
    Result := 'True';
  end
  else
  begin
    Result := 'False';
  end;
end;

function UnInstallOldVersion(): Integer;
var
  sUnInstallString: String;
  iResultCode: Integer;
begin
  Result := 0;
  sUnInstallString := GetUninstallString();
  Log('Uninstalling old version: ' + sUnInstallString);

  if sUnInstallString <> '' then
  begin
    sUnInstallString := RemoveQuotes(sUnInstallString);
    if Exec(sUnInstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, iResultCode) and (iResultCode = 0) then
      Result := 3
    else
      Result := 2;
  end
  else
    Result := 1;

  Log('UnInstallOldVersion.Result = ' + IntToStr(Result));
end;

function NumericVersion(const Version: string): Int64;
var
  Parts: TStringList;
  Major, Minor, Patch: Integer;
begin
  Parts := TStringList.Create;
  try
    Parts.StrictDelimiter := True;
    Parts.Delimiter := '.';
    Parts.DelimitedText := Version;

    Major := 0;
    Minor := 0;
    Patch := 0;

    if Parts.Count > 0 then Major := StrToIntDef(Parts[0], 0);
    if Parts.Count > 1 then Minor := StrToIntDef(Parts[1], 0);
    if Parts.Count > 2 then Patch := StrToIntDef(Parts[2], 0);

    Result := Int64(Major) * 1000000 + Int64(Minor) * 1000 + Int64(Patch);
  finally
    Parts.Free;
  end;
end;

function InitializeSetup(): Boolean;
var
  sInstalledVersion, message: String;
  installedVersion, myAppVersion: Int64;
  v: Integer;
begin
  Result := True;
  sInstalledVersion := GetInstalledVersion();

  if IsUpgrade() and (sInstalledVersion <> '') then
  begin
    Log('InitializeSetup.InstalledVersion: ' + sInstalledVersion);
    installedVersion := NumericVersion(sInstalledVersion);
    myAppVersion :=  NumericVersion(ExpandConstant('{#MyAppVersion}'));
    message := '';

    if installedVersion < myAppVersion  then
    begin
      message := 'An older version of Servy is already installed. Would you like to upgrade to this newer version?';
    end
    else if installedVersion > myAppVersion then
    begin
      message := 'A newer version of Servy is already installed. Are you sure you want to downgrade to this older version?';
    end
    else
    begin
      message := 'The same version of Servy is already installed. Would you like to reinstall it?';
    end;

    if WizardSilent then
    begin
      // Auto-accept in silent mode
      v := IDYES;
    end
    else
    begin
      // Interactive mode: show dialog
      v := MsgBox(message, mbInformation, MB_YESNO);
    end;

    if v <> IDYES then
    begin
      Result := False;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  sNeedsRestart : String;
begin
  sNeedsRestart := BoolToStr(NeedsRestart);
  Log('PrepareToInstall(' + sNeedsRestart + ') called');
  if IsUpgrade() then
  begin
    if UnInstallOldVersion() <> 3 then
    begin
      if not WizardSilent then
        MsgBox('Failed to uninstall the previous version.', mbError, MB_OK);

      Result := 'Failed to uninstall previous version';
      Exit;
    end;
  end;
  Result := '';
end;

// -----------------------------------------------------
// Post-Install actions:
//  - Add Servy to PATH if related task selected
//  - Refresh icon cache after install
//  - Replace XML Placeholders for Task Scheduler paths
// -----------------------------------------------------
// Declare Windows API function for refreshing icon cache
procedure SHChangeNotify(wEventId, uFlags: LongWord; dwItem1, dwItem2: LongWord); external 'SHChangeNotify@shell32.dll stdcall';

// Refresh icon cache after install
procedure RefreshIconCache();
begin
  SHChangeNotify($8000000, $0, 0, 0); // SHCNE_ASSOCCHANGED = $8000000
end;

const
  WM_SETTINGCHANGE = $001A;
  SMTO_ABORTIFHUNG = $0002;

function SendMessageTimeout(hWnd: LongWord; Msg: LongWord; wParam: LongWord;
  lParam: string; fuFlags: LongWord; uTimeout: LongWord; var lpdwResult: LongWord): LongWord;
  external 'SendMessageTimeoutW@user32.dll stdcall';

procedure RefreshEnvironment;
var
  ResultCode: LongWord;
begin
  SendMessageTimeout(
    HWND_BROADCAST,
    WM_SETTINGCHANGE,
    0,
    'Environment',        // pass string directly
    SMTO_ABORTIFHUNG,
    5000,
    ResultCode
  );
end;

// Removes trailing backslash
function NormalizeFolder(const S: string): string;
begin
  Result := S;
  if (Length(Result) > 0) and (Result[Length(Result)] = '\') then
    SetLength(Result, Length(Result) - 1);
end;

procedure SplitPath(const Value: string; Parts: TStringList);
begin
  Parts.StrictDelimiter := True;
  Parts.Delimiter := ';';
  Parts.DelimitedText := Value;
end;

function IsSameFolder(const Entry, NormalizedFolder: string): Boolean;
begin
  Result := CompareText(NormalizeFolder(Trim(Entry)), NormalizedFolder) = 0;
end;

function PathContainsFolder(const OldPath, NormalizedFolder: string): Boolean;
var
  Parts: TStringList;
  i: Integer;
begin
  Result := False;
  Parts := TStringList.Create;
  try
    SplitPath(OldPath, Parts);
    for i := 0 to Parts.Count - 1 do
      if IsSameFolder(Parts[i], NormalizedFolder) then
      begin
        Result := True;
        Exit;
      end;
  finally
    Parts.Free;
  end;
end;

procedure AddToPath(const Folder: string);
var
  OldPath, NewPath, NormalizedFolder: string;
  Parts: TStringList;
  i: Integer;
begin
  // Read the current system PATH
  if not RegQueryStringValue(HKLM64, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', OldPath) then
    OldPath := '';

  NormalizedFolder := NormalizeFolder(Folder);

  Parts := TStringList.Create;
  try
    SplitPath(OldPath, Parts);

    // Drop empty elements the existing value may already carry
    for i := Parts.Count - 1 downto 0 do
      if Trim(Parts[i]) = '' then
        Parts.Delete(i);

    if not PathContainsFolder(OldPath, NormalizedFolder) then
    begin
      Parts.Add(NormalizedFolder);
      NewPath := Parts.DelimitedText;

      // Write the new system PATH preserving REG_EXPAND_SZ type
      if not RegWriteExpandStringValue(HKLM64, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', NewPath) then
      begin
        Log('Failed to update system PATH environment variable.');
        if not WizardSilent then
          MsgBox('Failed to update system PATH environment variable.', mbError, MB_OK);
        Exit;
      end;

      // Notify the system about the environment change
      RefreshEnvironment();
    end;
  finally
    Parts.Free;
  end;
end;

function XmlEscape(const S: string): string;
begin
  Result := S;
  StringChangeEx(Result, '&', '&amp;', True);   // must run first
  StringChangeEx(Result, '<', '&lt;', True);
  StringChangeEx(Result, '>', '&gt;', True);
end;

procedure SecureDataDirectory();
var
  DataDir, Params: string;
  ResultCode: Integer;
begin
  DataDir := ExpandConstant('{commonappdata}\Servy');

  // Reset inheritance, grant Admins/System, and PURGE broad groups
  // *S-1-5-32-544: Administrators
  // *S-1-5-18:     Local System
  // *S-1-5-32-545: Users (Purge)
  // *S-1-5-11:     Authenticated Users (Purge)
  // *S-1-1-0:      Everyone (Purge)
  //
  // NOTE: Replicates SecurityHelper.ApplySecurityRules: No personal user ACE is granted here
  // because an elevated installer's user is always covered by the BUILTIN\Administrators grant.
  Params := Format('"%s" /inheritance:r /grant:r *S-1-5-32-544:(OI)(CI)F *S-1-5-18:(OI)(CI)F /remove:g *S-1-5-32-545 /remove:g *S-1-5-11 /remove:g *S-1-1-0 /remove:g "%s"', [DataDir, ExpandConstant('{username}')]);

  Log('Securing service data directory: icacls.exe ' + Params);
  if not Exec(ExpandConstant('{sys}\icacls.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
  begin
    Log(Format('WARNING: icacls.exe failed with exit code %d while hardening data directory "%s".', [ResultCode, DataDir]));
    if not WizardSilent then
      MsgBox('Failed to secure service data directory permissions. Please review system ACLs manually.', mbInformation, MB_OK);
  end;
end;

procedure CleanupOldSetupRegistryState();
var
  UninstKey: String;
  Hives: array[0..2] of Integer;
  Values: array[0..4] of String;
  i, j: Integer;
begin
  UninstKey := ExpandConstant('SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1');

  Hives[0] := HKLM64;
  Hives[1] := HKLM32;
  Hives[2] := HKCU;

  Values[0] := 'Inno Setup: Selected Tasks';
  Values[1] := 'Inno Setup: Deselected Tasks';
  Values[2] := 'Inno Setup: Selected Components';
  Values[3] := 'Inno Setup: Deselected Components';
  Values[4] := 'Inno Setup: Setup Type';

  for i := 0 to High(Hives) do
    for j := 0 to High(Values) do
      if RegValueExists(Hives[i], UninstKey, Values[j]) then
        RegDeleteValue(Hives[i], UninstKey, Values[j]);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  InstallDir: string;
  FileLines: TArrayOfString;
  FilesToFix: array[0..1] of String;
  I, J: Integer;
  InstallPath, XmlInstallPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    // 1. Refresh icon cache
    RefreshIconCache();

    // 2. Secure service data directory
    SecureDataDirectory();

    // 3. Clean up legacy remembered setup state post-install
    CleanupOldSetupRegistryState();

    // 4. Add to PATH logic (if selected)
    if WizardIsTaskSelected('addpath') and WizardIsComponentSelected('install_cli') then
    begin
      InstallDir := NormalizeFolder(ExpandConstant('{app}'));
      AddToPath(InstallDir);
      if not RegWriteDWordValue(HKLM64, 'Software\Servy', 'AddedToPath', 1) then
        Log('WARNING: Failed to write AddedToPath registry marker.');
    end;

    // 5. Resolve Hardcoded Paths in Task Scheduler XMLs
    InstallPath := ExpandConstant('{app}');
    XmlInstallPath := XmlEscape(InstallPath);
    FilesToFix[0] := InstallPath + '\taskschd\ServyFailureNotification.xml';
    FilesToFix[1] := InstallPath + '\taskschd\ServyFailureEmail.xml';

    for I := 0 to High(FilesToFix) do
    begin
      if FileExists(FilesToFix[I]) then
      begin
        if LoadStringsFromFile(FilesToFix[I], FileLines) then
        begin
          for J := 0 to GetArrayLength(FileLines) - 1 do
          begin
            StringChangeEx(FileLines[J], '{SERVY_INSTALL_PATH}', XmlInstallPath, True);
          end;

          // Written as UTF-8 with a BOM, which Task Scheduler accepts for task XML.
          if not SaveStringsToUTF8File(FilesToFix[I], FileLines, False) then
            Log('Failed to save updated Task Scheduler XML: ' + FilesToFix[I]);
        end
        else
          Log('Failed to load Task Scheduler XML for substitution: ' + FilesToFix[I]);
      end;
    end;
  end;
end;

// -----------------------------------------------------
// Uninstall actions:
//  - Remove Servy from PATH if necessary
//  - Clean up marker registry keys
// -----------------------------------------------------
procedure RemoveFromPath(const Folder: string);
var
  OldPath, NewPath: string;
  Parts: TStringList;
  i, InitialCount: Integer;
  NormalizedFolder: string;
begin
  NormalizedFolder := NormalizeFolder(Folder);

  if RegQueryStringValue(
        HKLM64,
        'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
        'Path',
        OldPath) then
  begin
    Parts := TStringList.Create;
    try
      SplitPath(OldPath, Parts);
      InitialCount := Parts.Count;

      for i := Parts.Count - 1 downto 0 do
        if IsSameFolder(Parts[i], NormalizedFolder) then
          Parts.Delete(i);

      if Parts.Count <> InitialCount then
      begin
        NewPath := Parts.DelimitedText;

        if not RegWriteExpandStringValue(
          HKLM64,
          'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
          'Path',
          NewPath
        ) then
          Log('WARNING: Failed to write updated PATH environment variable during uninstall.');

        RegWriteDWordValue(HKLM64, 'Software\Servy', 'AddedToPath', 0);

        RefreshEnvironment();
      end;
    finally
      Parts.Free;
    end;
  end;
end;

// PATH removal and registry cleanup on uninstall
procedure CurUninstallStepChanged(Step: TUninstallStep);
var
  AddedToPath: Cardinal;
begin
  if Step = usUninstall then
  begin
    if RegQueryDWordValue(HKLM64, 'Software\Servy', 'AddedToPath', AddedToPath) then
    begin
      if AddedToPath = 1 then
      begin
        RemoveFromPath(ExpandConstant('{app}'));
        Log('RemoveFromPath("' + ExpandConstant('{app}') + '")');
      end;
    end;
  end;

  if Step = usPostUninstall then
  begin
    RegDeleteKeyIncludingSubkeys(HKLM64, 'Software\Servy');
  end;
end;
