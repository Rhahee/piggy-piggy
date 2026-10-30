param([string]$Dotnet = '')
$ErrorActionPreference = 'Stop'
if (-not $Dotnet) {
    $runtimeCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    $Dotnet = if ($runtimeCommand) { $runtimeCommand.Source } else { Join-Path $env:USERPROFILE '.dotnet10\dotnet.exe' }
}
Push-Location $PSScriptRoot
try {
    & $Dotnet run --project '.\Tests\MyPiggyBank.Tests.csproj' -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & $Dotnet run --project '.\Tests\Wpf\MyPiggyBank.WpfChecks.csproj' -c Release
    if ($LASTEXITCODE -ne 0) { throw 'WPF checks failed.' }
    & $Dotnet publish '.\MyPiggyBank.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o '.\dist\Piggy - Piggy'
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath '.\README.md' -Destination '.\dist\Piggy - Piggy\README.md' -Force
    Copy-Item -LiteralPath '.\Assets\Icons\LICENSE.txt' -Destination '.\dist\Piggy - Piggy\Fluent-Icons-LICENSE.txt' -Force
    $releaseVersion = ([xml](Get-Content -LiteralPath '.\MyPiggyBank.csproj' -Raw)).Project.PropertyGroup.Version
    $releaseZip = '.\dist\Piggy-Piggy-' + $releaseVersion + '-win-x64.zip'
    Compress-Archive -LiteralPath '.\dist\Piggy - Piggy\Piggy - Piggy.exe','.\dist\Piggy - Piggy\README.md','.\dist\Piggy - Piggy\Fluent-Icons-LICENSE.txt' -DestinationPath $releaseZip -Force
    Get-FileHash -LiteralPath '.\dist\Piggy - Piggy\Piggy - Piggy.exe',$releaseZip -Algorithm SHA256
} finally { Pop-Location }
