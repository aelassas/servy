#define MyAppName "Servy"
#ifndef MyAppVersion
  #define MyAppVersion "1.0"  ; default if not provided
#endif
#ifndef MyAppPlatform
  #define MyAppPlatform "net48"  ; default if not provided
#endif
#define MyAppPublisher "Akram El Assas"
#define MyAppURL "https://servy-win.github.io/"
#define DocsURL "https://github.com/aelassas/servy/wiki"
#define MyAppExeName "Servy.exe"

#define ManagerAppName "Servy Manager"
#define ManagerAppExeName "Servy.Manager.exe"

#define CliExeName "servy-cli.exe"

[Setup]
PrivilegesRequired=admin
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{8343B121-BE1C-463F-AA5B-FD237DD2F8D0}
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
OutputBaseFilename=servy-{#MyAppVersion}-{#MyAppPlatform}-x64-installer
SetupIconFile=..\src\Servy\servy.ico

;Compression=lzma
;LZMAAlgorithm=1
;LZMADictionarySize=32768
;LZMADictionarySize=65536
;LZMADictionarySize=131072
;LZMANumFastBytes=273
;LZMAUseSeparateProcess=yes
;SolidCompression=yes

; Compression=bzip/1
; SolidCompression=yes

Compression=lzma2
LZMAAlgorithm=1
; LZMADictionarySize=32768
LZMADictionarySize=65536
; LZMADictionarySize=98304
; LZMADictionarySize=131072
;LZMADictionarySize=196608
LZMANumFastBytes=273
LZMAUseSeparateProcess=yes
SolidCompression=yes

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern dynamic

UsePreviousTasks=no
AlwaysRestart=no

[Messages]
SetupAppRunningError=Setup has detected that %1 is currently running.%n%nPlease close all instances of it now, then click OK to continue, or Cancel to exit.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation";
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "install_main_app"; Description: "Install Desktop App ({#MyAppExeName})"; Types: full
Name: "install_cli"; Description: "Install CLI ({#CliExeName})"; Types: full custom
Name: "install_manager"; Description: "Install Manager App ({#ManagerAppExeName})"; Types: full custom

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "Additional Options"; Flags: checkablealone
Name: "addpath"; Description: "Add Servy to PATH"; GroupDescription: "Additional Options"; Flags: checkablealone; Components: install_cli

[Files]
Source: "..\src\Servy\bin\x64\Release\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion; Components: install_main_app
; Source: "..\src\Servy\bin\x64\Release\{#MyAppExeName}.config"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall; Components: install_main_app
Source: "..\src\Servy\bin\x64\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion; Components: install_main_app

; cli
Source: "..\src\Servy.CLI\bin\x64\Release\Servy.CLI.exe"; DestDir: "{app}"; DestName:"{#CliExeName}"; Flags: ignoreversion; Components: install_cli
; Source: "..\src\Servy.CLI\bin\x64\Release\Servy.CLI.exe.config"; DestDir: "{app}"; DestName:"servy-cli.exe.config"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall; Components: install_cli
Source: "..\src\Servy.CLI\bin\x64\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli

; PowerShell Module
Source: "..\src\Servy.CLI\Servy.psm1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli
Source: "..\src\Servy.CLI\servy-module-examples.ps1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli

; Manager
Source: "..\src\Servy.Manager\bin\x64\Release\{#ManagerAppExeName}"; DestDir: "{app}"; Flags: ignoreversion; Components: install_manager
Source: "..\src\Servy.Manager\bin\x64\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion; Components: install_manager

; taskschd
Source: ".\taskschd\*"; DestDir: "{app}\taskschd"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall

; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Dirs]
Name: "{commonappdata}\Servy"; Permissions: networkservice-modify service-modify

[Icons]
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
Filename: "taskkill"; Parameters: "/im ""{#MyAppExeName}"" /t /f"; Flags: runhidden waituntilterminated; RunOnceId: StopMainApp
Filename: "taskkill"; Parameters: "/im ""{#ManagerAppExeName}"" /t /f"; Flags: runhidden waituntilterminated; RunOnceId: StopManagerApp
Filename: "taskkill"; Parameters: "/im ""{#CliExeName}"" /t /f"; Flags: runhidden waituntilterminated; RunOnceId: StopCliApp

[UninstallDelete]
Type: filesandordirs; Name: "{app}\x86"
Type: filesandordirs; Name: "{app}\x64"
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
function GetUninstallString(): String;
var
  sUnInstPath, sUnInstallString: String;
begin
  sUnInstPath := ExpandConstant('SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1');
  sUnInstallString := '';

  if not RegQueryStringValue(HKLM64, sUnInstPath, 'UninstallString', sUnInstallString) then
  begin
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  end;

  if sUnInstallString = '' then
  begin
    sUnInstPath := ExpandConstant('SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1');

    if not RegQueryStringValue(HKLM32, sUnInstPath, 'UninstallString', sUnInstallString) then
    begin
      RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
    end;
  end;

  Result := sUnInstallString;
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
    if Exec(sUnInstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES','', SW_HIDE, ewWaitUntilTerminated, iResultCode) then 
      Result := 3
    else
      Result := 2;
  end
  else
    Result := 1;

  Log('UnInstallOldVersion.Result = ' + IntToStr(Result));
end;

function GetInstalledVersion(): String;
var
  sUnInstPath, sVersionString: String;
begin
  sVersionString := '';
  sUnInstPath := ExpandConstant('SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1');
  
  if not RegQueryStringValue(HKLM64, sUnInstPath, 'DisplayVersion', sVersionString) then
  begin
    RegQueryStringValue(HKCU, sUnInstPath, 'DisplayVersion', sVersionString);
  end;

  if sVersionString = '' then
  begin
    sUnInstPath := ExpandConstant('SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1');
  
    if not RegQueryStringValue(HKLM32, sUnInstPath, 'DisplayVersion', sVersionString) then
    begin
      RegQueryStringValue(HKCU, sUnInstPath, 'DisplayVersion', sVersionString);
    end;
  end;
  
  Result := sVersionString;
end;

function NumericVersion(const Version: string): Integer;
var
  Parts: TStringList;
begin
  Parts := TStringList.Create;
  try
    Parts.Delimiter := '.';
    Parts.DelimitedText := Version;

    Result :=
      StrToIntDef(Parts[0], 0) * 1000 +
      StrToIntDef(Parts[1], 0);
  finally
    Parts.Free;
  end;
end;

// 528040 is the release key for .NET Framework 4.8
const DotNet48Release = 528040;

// Checks if .NET Framework 4.8 or newer is installed on the target machine
function IsDotNet48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= DotNet48Release);
end;

function InitializeSetup(): Boolean;
var
  sInstalledVersion, message: String;
  installedVersion, myAppVersion: Integer;
  v: Integer;
  UninstKey: String;
  Hives: array[0..1] of Integer;
  Values: array[0..4] of String;
  i, j: Integer;
begin
  Result := True;
 
  if not IsDotNet48Installed then
  begin
    MsgBox(
      'Servy requires .NET Framework 4.8 or newer. Please install it and try again.',
      mbCriticalError,
      MB_OK
    );
    Result := False;
    Exit;
  end;
 
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
  
  // Uninstall key path
  UninstKey := ExpandConstant('SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1');

  // Define the hives and value names to delete
  Hives[0] := HKLM64;
  Hives[1] := HKCU;
  Values[0] := 'Inno Setup: Selected Tasks';
  Values[1] := 'Inno Setup: Deselected Tasks';
  Values[2] := 'Inno Setup: Selected Components';
  Values[3] := 'Inno Setup: Deselected Components';
  Values[4] := 'Inno Setup: Setup Type';

  // Loop over hives and values to delete
  for i := 0 to High(Hives) do
    for j := 0 to High(Values) do
      if RegValueExists(Hives[i], UninstKey, Values[j]) then
        RegDeleteValue(Hives[i], UninstKey, Values[j]);
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
  
procedure AddToPath(const Folder: string);
var
  OldPath, NewPath, NormalizedFolder: string;
begin
  // Read the current system PATH
  if not RegQueryStringValue(HKLM64, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', OldPath) then
    OldPath := '';

  // Only add if it's not already there
  NormalizedFolder := NormalizeFolder(Folder);
  
  // Append semicolons to both sides for reliable checking against other entries (e.g., 'C:\A' vs 'C:\A B')
  if Pos(';' + LowerCase(NormalizedFolder) + ';', ';' + LowerCase(OldPath) + ';') = 0 then
  begin
    if OldPath <> '' then
        NewPath := OldPath + ';' + NormalizedFolder
    else
        NewPath := NormalizedFolder;

    // Write the new system PATH
    if not RegWriteStringValue(HKLM64, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', NewPath) then
    begin
      MsgBox('Failed to update system PATH environment variable.', mbError, MB_OK);
      Exit;
    end;

    // Notify the system about the environment change
    RefreshEnvironment();
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  InstallDir: string;
begin
  if CurStep = ssPostInstall then
  begin
    // Refresh icons
    RefreshIconCache();

    // Add to PATH if task selected
    if WizardIsTaskSelected('addpath') and WizardIsComponentSelected('install_cli') then
    begin
      InstallDir := NormalizeFolder(ExpandConstant('{app}'));
      AddToPath(InstallDir);
      RegWriteDWordValue(HKLM64, 'Software\Servy', 'AddedToPath', 1);
    end;
    
  end;
end;

// -----------------------------------------------------
// Uninstall actions:
//  - Remove Servy from PATH if necessary
// ----------------------------------------------------- 
procedure RemoveFromPath(const Folder: string);
var
  OldPath, NewPath: string;
  Parts: TStringList;
  i: Integer;
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
      Parts.StrictDelimiter := True;
      Parts.Delimiter := ';';
      Parts.DelimitedText := OldPath;

      for i := Parts.Count - 1 downto 0 do
        if CompareText(NormalizeFolder(Trim(Parts[i])), NormalizedFolder) = 0 then
          Parts.Delete(i);

      NewPath := Parts.DelimitedText;

      RegWriteStringValue(
        HKLM64,
        'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
        'Path',
        NewPath
      );
      
      RegWriteDWordValue(HKLM64, 'Software\Servy', 'AddedToPath', 0);

      RefreshEnvironment();
    finally
      Parts.Free;
    end;
  end;
end;

// PATH removal on uninstall
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
end;
