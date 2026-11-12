[CmdletBinding()]
param(
    [switch]$SkipTests,
    [string]$InnoSetupCompiler = ""
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'A Windows machine or Windows GitHub Actions runner is required to build WPF.'
}
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$version = '0.10.29'
$artifacts = Join-Path $root 'artifacts'
$publishDir = Join-Path $artifacts 'publish'
$distDir = Join-Path $artifacts 'dist'
$publishResolved = [System.IO.Path]::GetFullPath($publishDir)
$artifactResolved = [System.IO.Path]::GetFullPath($artifacts) + [System.IO.Path]::DirectorySeparatorChar
if (-not $publishResolved.StartsWith($artifactResolved, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Publish path must remain inside this project artifacts folder.' }
if (Test-Path -LiteralPath $publishResolved) { Remove-Item -LiteralPath $publishResolved -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir, $distDir -Force | Out-Null
$dotnet = Get-Command dotnet -ErrorAction Stop
Write-Host "Building AscendAPP $version for Windows x64" -ForegroundColor Cyan
if (-not $SkipTests) {
    & $dotnet.Source run --project (Join-Path $root 'tests/AscendAPP.SelfTest/AscendAPP.SelfTest.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Combat engine self-tests failed.' }
}
& $dotnet.Source publish (Join-Path $root 'src/AscendAPP.Desktop/AscendAPP.Desktop.csproj') `
    -c Release -r win-x64 --self-contained true -o $publishDir `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
$exe = Join-Path $publishDir 'FooMeter.exe'
if (-not (Test-Path $exe)) { throw "Publish did not produce $exe" }
Write-Host "App exe: $exe" -ForegroundColor Green

$compiler = $InnoSetupCompiler
if (-not $compiler) {
    $onPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($onPath) { $compiler = $onPath.Source }
}
if (-not $compiler) {
    foreach ($path in @(
        (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Inno Setup 6/ISCC.exe'),
        (Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Inno Setup 6/ISCC.exe'))
    ) { if (Test-Path $path) { $compiler = $path; break } }
}
if (-not $compiler -or -not (Test-Path $compiler)) {
    throw ('FooMeter.exe was published, but the installer requires Inno Setup 6. ' +
           'Install it from https://jrsoftware.org/isinfo.php and rerun this script.')
}
$script = Join-Path $PSScriptRoot 'AscendAPP.iss'
$icon = Join-Path $root 'src/AscendAPP.Desktop/Assets/FooMeter.ico'
& $compiler "/DAppVersion=$version" "/DAppPublishDir=$publishDir" "/DAppOutputDir=$distDir" "/DAppIconPath=$icon" $script
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
$setup = Join-Path $distDir "AscendAPP-Setup-$version-win-x64.exe"
if (-not (Test-Path $setup)) { throw "Installer not found: $setup" }
Write-Host "Installer: $setup" -ForegroundColor Green
Get-FileHash $exe, $setup -Algorithm SHA256 | Format-Table
