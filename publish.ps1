param(
    [string]$Runtime = "win-x64",
    [string]$Output = $(Join-Path $PSScriptRoot "dist"),
    [string]$ExeName = "dbshift.exe",
    [switch]$Clean
)

$project = Join-Path $PSScriptRoot "src\DbShift.CLI\DbShift.CLI.csproj"

if ($Clean -and (Test-Path $Output)) {
    Remove-Item -LiteralPath $Output -Recurse -Force
    Write-Host "Cleaned $Output"
}

Write-Host "Publishing dbshift ($Runtime)..." -ForegroundColor Cyan

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $Output `
    -p:PublishSingleFile=true

if ($LASTEXITCODE -eq 0) {
    $exe = Join-Path $Output $ExeName
    if (Test-Path $exe) {
        $size = (Get-Item $exe).Length / 1MB
        Write-Host "`nBinary created: $exe" -ForegroundColor Green
        Write-Host "Size: $([math]::Round($size, 2)) MB" -ForegroundColor Green
        Write-Host "`nTo use:" -ForegroundColor Cyan
        Write-Host "  $Output\$ExeName --help"
        Write-Host "`nExample:" -ForegroundColor Cyan
        Write-Host "  $Output\$ExeName new -n MyApp -p postgresql --json"
    } else {
        $anyExe = Get-ChildItem -LiteralPath $Output -Filter "*.exe" | Select-Object -First 1
        if ($anyExe) {
            $size = $anyExe.Length / 1MB
            Write-Host "`nBinary created: $($anyExe.FullName)" -ForegroundColor Green
            Write-Host "Size: $([math]::Round($size, 2)) MB" -ForegroundColor Green
        } else {
            Write-Host "`nPublish completed (check $Output for output files)" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "Publish failed." -ForegroundColor Red
    exit 1
}

# ── Supported runtimes ──────────────────────────────────
# win-x64, win-arm64
# linux-x64, linux-arm64, linux-musl-x64
# osx-x64, osx-arm64
