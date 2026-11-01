# NEXUS Build & Distribution Packaging Script
# Target: Windows 10 & 11 (x64)
# Obsidian Dark Universal File Processing & Conversion Platform

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  PROJECT NEXUS - Universal Processing Platform Build     " -ForegroundColor Cyan
Write-Host "  Any Format. One Destination.                            " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptRoot
$distDir = Join-Path $repoRoot "dist\nexus-portable"

Write-Host "`n[1/4] Preparing Output Directories..." -ForegroundColor Yellow
if (Test-Path $distDir) {
    Remove-Item -Recurse -Force $distDir
}
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

Write-Host "`n[2/4] Building Rust Processing Engine (Release)..." -ForegroundColor Yellow
Push-Location (Join-Path $repoRoot "processing-engine")
try {
    cargo build --workspace --release --offline
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Cargo offline release build failed or requires debug fallback."
    }
}
finally {
    Pop-Location
}

$engineRelease = Join-Path $repoRoot "processing-engine\target\release\nexus-engine.exe"
$engineDebug = Join-Path $repoRoot "processing-engine\target\debug\nexus-engine.exe"
$engineExe = if (Test-Path $engineRelease) { $engineRelease } else { $engineDebug }

if (-not (Test-Path $engineExe)) {
    throw "NEXUS Processing Engine binary not found!"
}
Write-Host "Found Processing Engine binary at: $engineExe" -ForegroundColor Green

Write-Host "`n[3/4] Publishing NEXUS Desktop UI (.NET)..." -ForegroundColor Yellow
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:DOTNET_NOLOGO = 1

$desktopProject = Join-Path $repoRoot "src\NEXUS.Desktop\NEXUS.Desktop.csproj"
dotnet publish $desktopProject -c Release -m:1 --no-restore -o $distDir

Write-Host "`n[4/4] Assembling Portable Bundle..." -ForegroundColor Yellow
# Copy processing engine into distribution directory
Copy-Item -Path $engineExe -Destination (Join-Path $distDir "nexus-engine.exe") -Force

# Copy Assets and Docs
$assetsDir = Join-Path $repoRoot "assets"
if (Test-Path $assetsDir) {
    Copy-Item -Path $assetsDir -Destination (Join-Path $distDir "assets") -Recurse -Force
}

$readme = Join-Path $repoRoot "README.md"
if (Test-Path $readme) {
    Copy-Item -Path $readme -Destination $distDir -Force
}

$license = Join-Path $repoRoot "LICENSE"
if (Test-Path $license) {
    Copy-Item -Path $license -Destination $distDir -Force
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  NEXUS Distribution Build Successfully Completed!        " -ForegroundColor Green
Write-Host "  Output Directory: $distDir" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

Get-ChildItem -Path $distDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
