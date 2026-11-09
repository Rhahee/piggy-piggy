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
#ifndef DocumentExtension
  #define DocumentExtension ".mpb"
#endif
#ifndef DocumentProgId
  #define DocumentProgId "Piggy.Document"
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
ChangesAssociations=yes
VersionInfoVersion=1.0.0.1
VersionInfoProductTextVersion=1.0.0.1
UninstallDisplayName={#ProductName}

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "fileassociation"; Description: "Register .mpb documents with Piggy"; GroupDescription: "File associations:"

[Files]
Source: "{#HelperDir}\Piggy.Setup.Download.exe"; Flags: dontcopy
Source: "{tmp}\payload\Piggy - Piggy.exe"; DestDir: "{app}"; Flags: external ignoreversion; BeforeInstall: BeforePayloadInstall
Source: "{tmp}\payload\README.md"; DestDir: "{app}"; Flags: external ignoreversion
Source: "{tmp}\payload\Fluent-Icons-LICENSE.txt"; DestDir: "{app}"; Flags: external ignoreversion
Source: "..\Assets\mpb.ico"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKCU; Subkey: "Software\Classes\{#DocumentProgId}"; ValueType: string; ValueName: ""; ValueData: "Piggy Document"; Flags: uninsdeletekey; Tasks: fileassociation
Root: HKCU; Subkey: "Software\Classes\{#DocumentProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\mpb.ico"",0"; Tasks: fileassociation
Root: HKCU; Subkey: "Software\Classes\{#DocumentProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Piggy - Piggy.exe"" ""%1"""; Tasks: fileassociation
Root: HKCU; Subkey: "Software\Classes\{#DocumentExtension}\OpenWithProgids"; ValueType: string; ValueName: "{#DocumentProgId}"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: fileassociation
; Do not log unconditional deletion of the default value: the user may change it later.
Root: HKCU; Subkey: "Software\Classes\{#DocumentExtension}"; ValueType: string; ValueName: ""; ValueData: "{#DocumentProgId}"; Tasks: fileassociation; Check: CanSetDocumentDefault
; Keep ownership across reinstalls that opt out. Capture it before uninstall
; removes registry entries, and never infer ownership just from the ProgID.
Root: HKCU; Subkey: "Software\Piggy\Installer\{#ProductId}"; ValueType: string; ValueName: "DocumentDefaultOwner"; ValueData: "{app}"; Flags: uninsdeletekey; Tasks: fileassociation; Check: CanSetDocumentDefault

[Icons]
Name: "{userprograms}\{#ProductName}"; Filename: "{app}\Piggy - Piggy.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\{#ProductName}"; Filename: "{app}\Piggy - Piggy.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Piggy - Piggy.exe"; Description: "Launch Piggy"; Flags: nowait postinstall skipifsilent

[Code]
var
  PayloadVersion: String;
  DownloadPage: TDownloadWizardPage;
  OwnsDocumentDefault: Boolean;

function CanSetDocumentDefault: Boolean;
var
  Existing: String;
begin
  // HKCR includes other per-user and machine-level registrations. UserChoice is
  // managed by Windows; setup never reads, deletes or rewrites its protected hash.
  Result := not RegQueryStringValue(HKCR, '{#DocumentExtension}', '', Existing);
  if not Result then Result := (Existing = '') or (Existing = '{#DocumentProgId}');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Existing, Owner: String;
begin
  if CurUninstallStep = usUninstall then begin
    OwnsDocumentDefault := False;
    if RegQueryStringValue(HKCU, 'Software\Piggy\Installer\{#ProductId}', 'DocumentDefaultOwner', Owner) then
      OwnsDocumentDefault := CompareText(Owner, ExpandConstant('{app}')) = 0;
  end;
  if (CurUninstallStep = usPostUninstall) and OwnsDocumentDefault then begin
    if RegQueryStringValue(HKCU, 'Software\Classes\{#DocumentExtension}', '', Existing) then
      if Existing = '{#DocumentProgId}' then begin
        RegDeleteValue(HKCU, 'Software\Classes\{#DocumentExtension}', '');
        RegDeleteKeyIfEmpty(HKCU, 'Software\Classes\{#DocumentExtension}');
      end;
  end;
end;

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
