#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the WorkLogManager Windows installer end-to-end: publishes the API
    (self-contained, win-x64), builds the front-end production bundle, copies it into
    the API's wwwroot, then compiles the Inno Setup installer.

.DESCRIPTION
    Must be run on Windows, with the .NET 10 SDK, Node.js/pnpm and Inno Setup (ISCC.exe)
    installed. See installer/README.md for setup instructions.

.PARAMETER Version
    Version number to stamp on the installer output file name and AppVersion
    (e.g. "1.0.0"). Defaults to "0.0.0" if not provided.

.EXAMPLE
    ./build.ps1 -Version 1.0.0
#>

param(
    [string]$Version = "0.0.0"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ApiProject = Join-Path $RepoRoot "api/src/WorkLogManager.Api"
$AppDir = Join-Path $RepoRoot "app"
$PublishDir = Join-Path $PSScriptRoot "publish/api"
$IssFile = Join-Path $PSScriptRoot "WorkLogManager.iss"

Write-Host "=== 1/4: Cleaning previous build output ===" -ForegroundColor Cyan
if (Test-Path (Join-Path $PSScriptRoot "publish")) {
    Remove-Item (Join-Path $PSScriptRoot "publish") -Recurse -Force
}
if (Test-Path (Join-Path $PSScriptRoot "output")) {
    Remove-Item (Join-Path $PSScriptRoot "output") -Recurse -Force
}

Write-Host "=== 2/4: Publishing WorkLogManager.Api (win-x64, self-contained) ===" -ForegroundColor Cyan
dotnet publish $ApiProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Host "=== 3/4: Building front-end and copying to wwwroot ===" -ForegroundColor Cyan
Push-Location $AppDir
try {
    pnpm install --frozen-lockfile
    if ($LASTEXITCODE -ne 0) { throw "pnpm install failed." }

    pnpm run build
    if ($LASTEXITCODE -ne 0) { throw "pnpm run build failed." }
}
finally {
    Pop-Location
}

$WwwrootDir = Join-Path $PublishDir "wwwroot"
New-Item -ItemType Directory -Force -Path $WwwrootDir | Out-Null
Copy-Item (Join-Path $AppDir "dist/*") $WwwrootDir -Recurse -Force

Write-Host "=== 4/4: Compiling installer with Inno Setup ===" -ForegroundColor Cyan
$Iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if (-not $Iscc) {
    $DefaultIsccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (Test-Path $DefaultIsccPath) {
        $Iscc = $DefaultIsccPath
    }
    else {
        throw "ISCC.exe not found. Install Inno Setup 6 (https://jrsoftware.org/isinfo.php) or add ISCC.exe to PATH."
    }
}
else {
    $Iscc = $Iscc.Source
}

$env:WORKLOGMANAGER_VERSION = $Version
& $Iscc $IssFile
if ($LASTEXITCODE -ne 0) { throw "ISCC.exe compilation failed." }

Write-Host "=== Done. Installer generated in installer/output/ ===" -ForegroundColor Green
