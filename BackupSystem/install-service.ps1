# Скрипт установки службы Windows BackupSystem

param(
    [string]$ServiceName = "BackupSystem",
    [string]$DisplayName = "Система резервного копирования",
    [string]$InstallPath = "C:\Program Files\BackupSystem",
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

if ($Uninstall) {
    # Удаление службы
    Write-Host "Removing service..." -ForegroundColor Yellow
    
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
    
    sc.exe delete $ServiceName
    Write-Host "  Service removed!" -ForegroundColor Green
    
    # Удаление файлов
    if (Test-Path $InstallPath) {
        Remove-Item $InstallPath -Recurse -Force
        Write-Host "  Files removed from: $InstallPath" -ForegroundColor Green
    }
    
    Write-Host "Uninstall complete!" -ForegroundColor Green
    exit 0
}

# Проверка существования службы
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Service already exists. Updating..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
}

# Создание службы
Write-Host "Installing service..." -ForegroundColor Yellow

$binaryPath = "$InstallPath\BackupSystem.Service.exe"

if (-not (Test-Path $binaryPath)) {
    Write-Host "ERROR: Binary not found at: $binaryPath" -ForegroundColor Red
    Write-Host "Run build.ps1 -Publish -Install first" -ForegroundColor Yellow
    exit 1
}

# Регистрация службы через sc.exe
sc.exe create $ServiceName `
    binPath= "`"$binaryPath`"" `
    start= auto `
    DisplayName= "$DisplayName"

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to create service" -ForegroundColor Red
    exit 1
}

# Настройка восстановления при сбое
sc.exe failure $ServiceName `
    reset= 86400 `
    actions= restart/60000/restart/60000/restart/60000

# Запуск службы
Write-Host "Starting service..." -ForegroundColor Yellow
Start-Service -Name $ServiceName

# Проверка статуса
$service = Get-Service -Name $ServiceName
Write-Host ""
Write-Host "Service Status:" -ForegroundColor Cyan
Write-Host "  Name: $($service.Name)" -ForegroundColor White
Write-Host "  DisplayName: $($service.DisplayName)" -ForegroundColor White
Write-Host "  Status: $($service.Status)" -ForegroundColor $(if ($service.Status -eq 'Running') { 'Green' } else { 'Yellow' })
Write-Host "  StartType: $($service.StartType)" -ForegroundColor White

Write-Host ""
Write-Host "Service installed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Cyan
Write-Host "  Get-Service $ServiceName     # Check status" -ForegroundColor White
Write-Host "  Stop-Service $ServiceName    # Stop service" -ForegroundColor White
Write-Host "  Start-Service $ServiceName   # Start service" -ForegroundColor White
Write-Host "  .\install-service.ps1 -Uninstall  # Uninstall" -ForegroundColor White
