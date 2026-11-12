param([switch]$Release, [switch]$Installer)
if ($Installer) {
    & (Join-Path $PSScriptRoot 'packaging/Build-Installer.ps1')
    if (-not $?) { exit 1 }
    exit 0
}
$ErrorActionPreference = 'Stop'
$configuration = if ($Release) { 'Release' } else { 'Debug' }
dotnet restore .\AscendAPP.slnx -m:1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build .\AscendAPP.slnx -c $configuration --no-restore -m:1 -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project .\tests\AscendAPP.SelfTest -c $configuration --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host 'Ready: src\AscendAPP.Desktop\bin\' -ForegroundColor Green
