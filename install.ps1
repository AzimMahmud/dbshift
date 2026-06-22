#!/usr/bin/env pwsh
# DbShift – official Windows install script
# Usage:
#   powershell -c "iwr -Uri https://github.com/AzimMahmud/dbshift/releases/latest/download/install.ps1 | iex"
#   pwsh -c "iwr -Uri https://github.com/AzimMahmud/dbshift/releases/latest/download/install.ps1 | iex"

param(
    [string]$Repo = "AzimMahmud/dbshift",
    [string]$Version = "latest",
    [string]$InstallDir = "$env:LOCALAPPDATA\DbShift"
)

$ErrorActionPreference = "Stop"

function Info  { Write-Host "  > $_" -ForegroundColor Cyan }
function Ok    { Write-Host "  ✓ $_" -ForegroundColor Green }
function Warn  { Write-Host "  ⚠ $_" -ForegroundColor Yellow }
function Err   { Write-Host "  ✗ $_" -ForegroundColor Red; exit 1 }

# ── architecture detection ────────────────────────────────────────────────
$arch = switch ([Environment]::ProcessorArchitecture) {
    "X64"   { "x64" }
    "Arm64" { "arm64" }
    default { Err "Unsupported architecture: $([Environment]::ProcessorArchitecture)" }
}
$platform = "windows-$arch"

# ── download URL ──────────────────────────────────────────────────────────
if ($Version -eq "latest") {
    $url = "https://github.com/$Repo/releases/latest/download/dbshift-$platform.zip"
} else {
    $url = "https://github.com/$Repo/releases/download/v$Version/dbshift-$platform.zip"
}

# ── download ──────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  ╭──────────────────────────────────────╮" -ForegroundColor Cyan
Write-Host "  │  DbShift — database migration tool   │" -ForegroundColor Cyan
Write-Host "  ╰──────────────────────────────────────╯" -ForegroundColor Cyan
Write-Host ""

Info "Detected: $platform"
Info "Downloading dbshift for $platform..."

$zip = "$env:TEMP\dbshift.zip"
try {
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
} catch {
    Err "Download failed: $url`n  $_"
}
Ok "Downloaded"

# ── extract ───────────────────────────────────────────────────────────────
Info "Extracting..."
if (Test-Path $InstallDir) {
    Remove-Item -LiteralPath $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
}
$null = New-Item -ItemType Directory -Path $InstallDir -Force
try {
    Expand-Archive -Path $zip -DestinationPath $InstallDir -Force
} catch {
    Err "Extraction failed: $_"
}
Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
Ok "Extracted to $InstallDir"

# ── add to PATH ───────────────────────────────────────────────────────────
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($currentPath -notlike "*$InstallDir*") {
    $newPath = "$InstallDir;$currentPath"
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    # Also update current session
    $env:Path = "$InstallDir;$env:Path"
    Ok "Added to user PATH: $InstallDir"
} else {
    Info "$InstallDir already on PATH"
}

# ── verify ────────────────────────────────────────────────────────────────
$exe = Join-Path $InstallDir "dbshift.exe"
if (Test-Path $exe) {
    $version = & $exe --version 2>&1 | Select-Object -First 1
    Ok $version
} else {
    Warn "Binary not found at $exe"
}

Write-Host ""
Info "Run 'dbshift --help' to get started."
Write-Host ""
