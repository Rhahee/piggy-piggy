param(
    [string]$Dotnet = '',
    [string]$Iscc = '',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist\installer')
)
$ErrorActionPreference = 'Stop'
if (-not $Iscc) {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compiler) { $Iscc = $compiler.Source }
    else {
        $candidates = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe"
        )
        $Iscc = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
}
if (-not $Iscc -or -not (Test-Path -LiteralPath $Iscc -PathType Leaf)) {
    throw 'Install Inno Setup 6.7+ or supply -Iscc with the full path to ISCC.exe.'
}
$Iscc = (Resolve-Path -LiteralPath $Iscc).Path

if (-not $Dotnet) {
    $localSdk = Join-Path $env:USERPROFILE '.dotnet10\dotnet.exe'
    $Dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
}
# The installer embeds a verifier, not an application release. Application version
# and payload are resolved from the official channel every time setup runs.
& $Dotnet run --project (Join-Path $PSScriptRoot 'Tests\MyPiggyBank.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
& $Dotnet run --project (Join-Path $PSScriptRoot 'Tests\Wpf\MyPiggyBank.WpfChecks.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'WPF checks failed.' }
$helperDir = Join-Path $PSScriptRoot 'obj\installer-helper\win-x64'
& $Dotnet publish (Join-Path $PSScriptRoot 'installer\Downloader\Piggy.Setup.Download.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $helperDir
if ($LASTEXITCODE -ne 0) { throw 'Setup helper publish failed.' }
$outputDir = [IO.Path]::GetFullPath($OutputDirectory)
& $Iscc "/DHelperDir=$helperDir" "/DOutputDir=$outputDir" (Join-Path $PSScriptRoot 'installer\Piggy.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$installer = Join-Path $outputDir 'Piggy-Piggy-Web-Setup-win-x64.exe'
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$installer.sha256", "$hash  $([IO.Path]::GetFileName($installer))`n", [Text.UTF8Encoding]::new($false))
Get-Item -LiteralPath $installer,"$installer.sha256"
