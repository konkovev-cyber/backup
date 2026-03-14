# Build script for BackupSystem
$ErrorActionPreference = "Stop"

$solutionDir = Join-Path $PSScriptRoot "BackupSystem"
$publishDir = Join-Path $PSScriptRoot "publish"
$serviceProject = Join-Path $solutionDir "src\Service\BackupSystem.Service.csproj"
$uiProject = Join-Path $solutionDir "src\UI\BackupSystem.UI.csproj"

Write-Host "--- Starting BackupSystem Build ---" -ForegroundColor Cyan

# 1. Clean publish directory
if (Test-Path $publishDir) {
    Write-Host "Cleaning publish directory..."
    Remove-Item -Path $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir | Out-Null

# 2. Restore dependencies
Write-Host "Restoring dependencies..." -ForegroundColor Yellow
dotnet restore (Join-Path $solutionDir "BackupSystem.sln")

# 3. Build & Publish Service (Windows Service)
Write-Host "Publishing Service (Windows Service)..." -ForegroundColor Yellow
$serviceOut = Join-Path $publishDir "Service"
dotnet publish $serviceProject -c Release -o $serviceOut --self-contained false

# 4. Build & Publish UI (WPF Application)
Write-Host "Publishing UI (WPF Application)..." -ForegroundColor Yellow
$uiOut = Join-Path $publishDir "UI"
dotnet publish $uiProject -c Release -o $uiOut --self-contained false

# 5. Copy config samples
Write-Host "Copying configuration samples..." -ForegroundColor Yellow
if (Test-Path (Join-Path $solutionDir "backup.json")) {
    Copy-Item (Join-Path $solutionDir "backup.json") (Join-Path $publishDir "backup.sample.json")
}

Write-Host "`n--- Build Completed Successfully! ---" -ForegroundColor Green
Write-Host "Published files are in: $publishDir"
Write-Host "To install the service, run (as administrator):"
Write-Host "sc.exe create BackupSystem binPath= `"$($serviceOut)\BackupSystem.Service.exe`" start= auto"
