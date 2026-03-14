# Скрипт сборки BackupSystem
# Требуется: .NET 8.0 SDK

param(
    [string]$Configuration = "Release",
    [switch]$Publish,
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$OutputDir = Join-Path $ProjectRoot "publish"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  BackupSystem Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Проверка .NET SDK
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "  .NET SDK version: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR: .NET SDK not found!" -ForegroundColor Red
    Write-Host "  Download from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Восстановление пакетов
Write-Host ""
Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore $ProjectRoot\BackupSystem.sln

# Сборка
Write-Host ""
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build $ProjectRoot\BackupSystem.sln -c $Configuration --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "  Build FAILED!" -ForegroundColor Red
    exit 1
}

Write-Host "  Build SUCCESS!" -ForegroundColor Green

# Публикация
if ($Publish) {
    Write-Host ""
    Write-Host "Publishing..." -ForegroundColor Yellow
    
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
    }
    
    dotnet publish $ProjectRoot\BackupSystem.sln `
        -c $Configuration `
        -r win-x64 `
        --self-contained false `
        -o $OutputDir
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Publish SUCCESS!" -ForegroundColor Green
        Write-Host "  Output: $OutputDir" -ForegroundColor Cyan
    }
    else {
        Write-Host "  Publish FAILED!" -ForegroundColor Red
        exit 1
    }
}

# Установка
if ($Install) {
    if (-not $Publish) {
        Write-Host ""
        Write-Host "ERROR: -Publish required for -Install" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
    Write-Host "Installing..." -ForegroundColor Yellow
    
    $installPath = "C:\Program Files\BackupSystem"
    $dataPath = "C:\ProgramData\BackupSystem"
    
    # Создание директорий
    if (-not (Test-Path $installPath)) {
        New-Item -ItemType Directory -Path $installPath -Force | Out-Null
    }
    
    if (-not (Test-Path $dataPath)) {
        New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
        New-Item -ItemType Directory -Path "$dataPath\Logs" -Force | Out-Null
        New-Item -ItemType Directory -Path "$dataPath\Temp" -Force | Out-Null
    }
    
    # Копирование файлов
    Copy-Item "$OutputDir\*" $installPath -Recurse -Force
    
    # Копирование примера конфигурации
    if (-not (Test-Path "$dataPath\backup.json")) {
        Copy-Item "$ProjectRoot\config\backup.example.json" "$dataPath\backup.json"
        Write-Host "  Configuration created at: $dataPath\backup.json" -ForegroundColor Green
    }
    
    Write-Host "  Installed to: $installPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "To install as Windows Service, run:" -ForegroundColor Cyan
    Write-Host "  .\install-service.ps1" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Done!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
