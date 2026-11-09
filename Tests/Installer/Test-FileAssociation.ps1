param(
    [string]$Iscc = (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    [string]$PayloadDirectory = '',
    [string]$HelperDirectory = ''
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $PayloadDirectory) { $PayloadDirectory = Join-Path $workspace 'dist\Piggy - Piggy' }
if (-not $HelperDirectory) { $HelperDirectory = Join-Path $workspace 'obj\installer-helper\win-x64' }
foreach ($path in @($Iscc, (Join-Path $PayloadDirectory 'Piggy - Piggy.exe'), (Join-Path $HelperDirectory 'Piggy.Setup.Download.exe'))) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing prerequisite: $path. Run publish.ps1 and build-installer.ps1 first." }
}
$identity = [Guid]::NewGuid().ToString('N')
$extension = '.piggy-mpb-qa-' + $identity
$progId = 'Piggy.Document.QA.' + $identity
$appId = [Guid]::NewGuid().ToString().ToUpperInvariant()
$qa = Join-Path $workspace ('qa\file-association-' + $identity)
$installDir = Join-Path $qa 'installed app'
$uninstallKey = "Software\Microsoft\Windows\CurrentVersion\Uninstall\{$appId}_is1"
$ownerKey = 'Software\Piggy\Installer\'+$appId
$classes = 'Software\Classes\'
New-Item -ItemType Directory -Path $qa -Force | Out-Null
$script:passed = 0
function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:passed++
    Write-Output "PASS $message"
}
function Value([string]$path, [string]$name = '') {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($path)
    if ($null -eq $key) { return $null }
    try { $key.GetValue($name) } finally { $key.Dispose() }
}
function SetDefault([string]$value) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($classes+$extension)
    try { $key.SetValue('', $value) } finally { $key.Dispose() }
}
function Install([string]$Tasks = 'fileassociation') {
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /TASKS="'+$Tasks+'" /LOG="' + (Join-Path $qa ('install-'+$script:passed+'.log')) + '"'
    $process = Start-Process -FilePath (Join-Path $qa 'Piggy-Association-QA.exe') -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    Assert ($process.ExitCode -eq 0) 'isolated installation succeeds'
}
function Uninstall {
    $process = Start-Process -FilePath (Join-Path $installDir 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -Wait -PassThru
    Assert ($process.ExitCode -eq 0) 'isolated uninstall succeeds'
}
if (-not ('PiggyAssociationQuery' -as [type])) {
    Add-Type @'
using System.Runtime.InteropServices;
using System.Text;
public static class PiggyAssociationQuery {
    [DllImport("shlwapi.dll", CharSet=CharSet.Unicode)]
    public static extern uint AssocQueryString(uint flags, uint str, string assoc, string extra, StringBuilder output, ref uint length);
}
'@
}
function ShellAssociation([uint32]$kind) {
    $buffer = [Text.StringBuilder]::new(4096)
    [uint32]$length = $buffer.Capacity
    $result = [PiggyAssociationQuery]::AssocQueryString(0,$kind,$extension,'open',$buffer,[ref]$length)
    if ($result -ne 0) { throw "Windows association query failed: $result" }
    $buffer.ToString()
}

# Use the production registration/uninstall rules with an isolated identity and
# extension. Only the download preparation is replaced by a local test payload.
$source = Get-Content -LiteralPath (Join-Path $workspace 'installer\Piggy.iss') -Raw
$source = $source.Replace('..\Assets\app.ico',(Join-Path $workspace 'Assets\app.ico'))
$source = $source.Replace('..\Assets\mpb.ico',(Join-Path $workspace 'Assets\mpb.ico'))
$source = $source.Replace('AppMutex=Local\Piggy-Application', 'AppMutex=Local\Piggy-Association-QA-'+$identity)
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $PayloadDirectory 'Piggy - Piggy.exe')).FileVersion
$prepare = "function PrepareToInstall(var NeedsRestart: Boolean): String;`r`nbegin`r`n  Result := '';`r`n  PayloadVersion := '$version';`r`n  ForceDirectories(ExpandConstant('{tmp}\payload'));`r`n"
foreach ($name in @('Piggy - Piggy.exe','README.md','Fluent-Icons-LICENSE.txt')) {
    $inputPath = (Join-Path $PayloadDirectory $name).Replace("'", "''")
    $prepare += "  if not FileCopy('$inputPath', ExpandConstant('{tmp}\payload\$name'), False) then RaiseException('Could not copy QA payload');`r`n"
}
$prepare += "end;`r`n`r`n"
$start = $source.IndexOf('function PrepareToInstall(')
$end = $source.IndexOf('procedure BeforePayloadInstall;', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'Could not isolate installer download preparation.' }
$source = $source.Substring(0,$start)+$prepare+$source.Substring($end)
$scriptFile = Join-Path $qa 'Association-QA.iss'
[IO.File]::WriteAllText($scriptFile,$source,[Text.UTF8Encoding]::new($false))
& $Iscc /Q /FPiggy-Association-QA "/DHelperDir=$HelperDirectory" "/DOutputDir=$qa" "/DProductId=$appId" "/DProductName=Piggy Association QA $identity" "/DInstallDir=$installDir" "/DDocumentExtension=$extension" "/DDocumentProgId=$progId" $scriptFile
if ($LASTEXITCODE -ne 0) { throw 'QA installer compilation failed.' }
$sentinel = Join-Path $installDir 'preserve.mpb'
try {
    Assert ($null -eq (Value ($classes+$extension))) 'QA extension has no pre-existing owner'
    Install
    $expectedIcon = '"'+(Join-Path $installDir 'mpb.ico')+'",0'
    $expectedCommand = '"'+(Join-Path $installDir 'Piggy - Piggy.exe')+'" "%1"'
    Assert ((Value ($classes+$extension)) -eq $progId) 'unassigned file type is registered to Piggy'
    Assert ((Value ($classes+$progId+'\DefaultIcon')) -eq $expectedIcon) 'document icon points to the grayscale ICO'
    Assert ((ShellAssociation 15) -eq $expectedIcon) 'Windows Shell resolves the grayscale document icon'
    Assert ((ShellAssociation 1) -eq $expectedCommand) 'Windows Shell resolves the correctly quoted Open command'
    Assert ((Get-FileHash (Join-Path $installDir 'mpb.ico')).Hash -eq (Get-FileHash (Join-Path $workspace 'Assets\mpb.ico')).Hash) 'installed ICO matches the approved document asset'
    $ico = [IO.File]::ReadAllBytes((Join-Path $installDir 'mpb.ico'))
    Assert ([BitConverter]::ToUInt16($ico,4) -eq 9) 'document ICO contains all nine size frames'
    [IO.File]::WriteAllText($sentinel,'fictional file-preservation sentinel')
    $sentinelHash = (Get-FileHash $sentinel).Hash
    Uninstall
    Assert ($null -eq (Value ($classes+$progId))) 'uninstaller removes its document ProgID'
    Assert ($null -eq (Value ($classes+$extension+'\OpenWithProgids') $progId)) 'uninstaller removes its Open With entry'
    Assert ($null -eq (Value ($classes+$extension))) 'uninstaller removes only its own extension default'
    Assert (-not (Test-Path (Join-Path $installDir 'mpb.ico'))) 'uninstaller removes the document ICO'
    Assert ((Get-FileHash $sentinel).Hash -eq $sentinelHash) 'uninstall preserves .mpb file bytes'

    SetDefault 'Other.Reader.QA'
    Install
    Assert ((Value ($classes+$extension)) -eq 'Other.Reader.QA') 'installation preserves another application default'
    Assert ($null -ne (Value ($classes+$extension+'\OpenWithProgids') $progId)) 'Piggy remains available through Open With'
    Uninstall
    Assert ((Value ($classes+$extension)) -eq 'Other.Reader.QA') 'uninstall preserves a pre-existing foreign default'

    SetDefault ''
    Install
    SetDefault 'User.Changed.Reader.QA'
    Uninstall
    Assert ((Value ($classes+$extension)) -eq 'User.Changed.Reader.QA') 'uninstall preserves a default changed after installation'
    Assert ((Get-FileHash $sentinel).Hash -eq $sentinelHash) 'all scenarios preserve the sentinel document'

    # Regression: an opt-out installation must not claim or remove an older
    # association whose ProgID happens to have the same name.
    SetDefault $progId
    $previous = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($classes+$progId+'\DefaultIcon')
    try { $previous.SetValue('', 'C:\Existing-Piggy-QA\document.ico,0') } finally { $previous.Dispose() }
    Install ''
    Assert ((Value ($classes+$extension)) -eq $progId) 'opt-out installation preserves a pre-existing Piggy default'
    Uninstall
    Assert ((Value ($classes+$extension)) -eq $progId) 'opt-out uninstall preserves a Piggy default not created by this installation'
    Assert ((Value ($classes+$progId+'\DefaultIcon')) -eq 'C:\Existing-Piggy-QA\document.ico,0') 'opt-out uninstall preserves the pre-existing icon registration'
    Assert ($null -eq (Value $ownerKey 'DocumentDefaultOwner')) 'opt-out installation never claims default ownership'

    SetDefault ''
    Install
    Assert ((Value $ownerKey 'DocumentDefaultOwner') -eq $installDir) 'registration records the owning installation directory'
    Install ''
    Assert ((Value $ownerKey 'DocumentDefaultOwner') -eq $installDir) 'reinstall opt-out retains ownership from the initial installation'
    Uninstall
    Assert ($null -eq (Value ($classes+$extension))) 'uninstall after reinstall cleans its originally owned default'
    Assert ($null -eq (Value $ownerKey 'DocumentDefaultOwner')) 'uninstall removes its ownership marker'
    Assert ((Get-FileHash $sentinel).Hash -eq $sentinelHash) 'regression scenarios preserve document bytes'
    Write-Output "All $script:passed file-association checks passed. Evidence: $qa"
}
finally {
    if ($null -ne (Value $uninstallKey 'UninstallString')) { Uninstall }
    # Names contain the random QA identity; no production .mpb key is touched.
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($classes+$extension,$false)
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($classes+$progId,$false)
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($ownerKey,$false)
}
