#ifndef HelperDir
  #error HelperDir must be supplied by build-installer.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by build-installer.ps1
#endif
#ifndef ProductId
  #define ProductId "7A751CF3-5E61-4FB3-BBDB-A09F77BF92DA"
#endif
#ifndef ProductName
  #define ProductName "Piggy"
#endif
#ifndef InstallDir
  #define InstallDir "{localappdata}\Programs\Piggy"
#endif

[Setup]
AppId={{{#ProductId}}
AppName={#ProductName}
AppVersion={code:GetPayloadVersion}
AppVerName={#ProductName} Web Setup
AppPublisher=Rhahee
AppPublisherURL=https://github.com/Rhahee/piggy-piggy
DefaultDirName={#InstallDir}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=Piggy-Piggy-Web-Setup-win-x64
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\Piggy - Piggy.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
AppMutex=Local\Piggy-Application
RestartApplications=no
VersionInfoVersion=1.0.0.0
VersionInfoProductTextVersion=1.0.0.0
UninstallDisplayName={#ProductName}

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#HelperDir}\Piggy.Setup.Download.exe"; Flags: dontcopy
Source: "{tmp}\payload\Piggy - Piggy.exe"; DestDir: "{app}"; Flags: external ignoreversion; BeforeInstall: BeforePayloadInstall
Source: "{tmp}\payload\README.md"; DestDir: "{app}"; Flags: external ignoreversion
Source: "{tmp}\payload\Fluent-Icons-LICENSE.txt"; DestDir: "{app}"; Flags: external ignoreversion

[Icons]
Name: "{userprograms}\{#ProductName}"; Filename: "{app}\Piggy - Piggy.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\{#ProductName}"; Filename: "{app}\Piggy - Piggy.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Piggy - Piggy.exe"; Description: "Launch Piggy"; Flags: nowait postinstall skipifsilent

[Code]
var
  PayloadVersion: String;
  DownloadPage: TDownloadWizardPage;

function GetPayloadVersion(Param: String): String;
begin
  Result := PayloadVersion;
  if Result = '' then Result := '0.0.0.0';
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
var
  Limit: Int64;
begin
  Limit := 250 * 1024 * 1024;
  if FileName = 'package.sha256' then Limit := 4096;
  Result := (Progress <= Limit) and (ProgressMax <= Limit);
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage('Downloading Piggy',
    'Internet is required. Your existing application stays in place until verification succeeds.', @OnDownloadProgress);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
end;

function RunHelper(Operation: String): String;
var
  ExitCode: Integer;
  ErrorText: AnsiString;
begin
  Result := '';
  DeleteFile(ExpandConstant('{tmp}\error.txt'));
  if not Exec(ExpandConstant('{tmp}\Piggy.Setup.Download.exe'),
    Operation + ' "' + ExpandConstant('{tmp}') + '"', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    Result := 'Could not start the package verification helper.'
  else if ExitCode <> 0 then begin
    Result := 'Could not verify the release. Check your connection and try again.';
    if LoadStringFromFile(ExpandConstant('{tmp}\error.txt'), ErrorText) then
      Result := String(ErrorText);
  end;
end;

function DowngradeError: String;
var
  ExistingMS, ExistingLS, DownloadMS, DownloadLS: Cardinal;
  Target: String;
begin
  Result := '';
  Target := ExpandConstant('{app}\Piggy - Piggy.exe');
  if FileExists(Target) then begin
    if not GetVersionNumbers(Target, ExistingMS, ExistingLS) then begin
      Result := 'Could not read the installed application version. The existing application was preserved.';
      Exit;
    end;
    if not GetVersionNumbers(ExpandConstant('{tmp}\payload\Piggy - Piggy.exe'), DownloadMS, DownloadLS) then begin
      Result := 'Could not read the downloaded application version.';
      Exit;
    end;
    if (ExistingMS > DownloadMS) or ((ExistingMS = DownloadMS) and (ExistingLS > DownloadLS)) then
      Result := 'A newer version of Piggy is already installed. Setup will not downgrade it.';
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  PackageUrl, ChecksumUrl: String;
begin
  Result := '';
  try
    ExtractTemporaryFile('Piggy.Setup.Download.exe');
    WizardForm.StatusLabel.Caption := 'Checking the official Piggy release channel...';
    Result := RunHelper('resolve');
    if Result <> '' then Exit;
    PayloadVersion := GetIniString('Release', 'Version', '', ExpandConstant('{tmp}\release.ini'));
    PackageUrl := GetIniString('Release', 'Package', '', ExpandConstant('{tmp}\release.ini'));
    ChecksumUrl := GetIniString('Release', 'Checksum', '', ExpandConstant('{tmp}\release.ini'));
    if (PayloadVersion = '') or (PackageUrl = '') or (ChecksumUrl = '') then begin
      Result := 'The release information is incomplete.';
      Exit;
    end;
    DownloadPage.Clear;
    DownloadPage.Add(ChecksumUrl, 'package.sha256', '');
    DownloadPage.Add(PackageUrl, 'package.zip', '');
    DownloadPage.Show;
    try
      DownloadPage.Download;
    finally
      DownloadPage.Hide;
    end;
    // Retry after an error must not reuse partially staged files.
    DelTree(ExpandConstant('{tmp}\payload'), True, True, True);
    Result := RunHelper('stage');
    if Result <> '' then Exit;
    Result := DowngradeError;
  except
    Result := GetExceptionMessage;
  end;
end;

procedure BeforePayloadInstall;
var
  ErrorText: String;
begin
  // Repeat immediately before copying in case another updater finished meanwhile.
  ErrorText := DowngradeError;
  if ErrorText <> '' then RaiseException(ErrorText);
end;
