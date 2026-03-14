# Система резервного копирования

Приложение для автоматизации резервного копирования данных под управлением Windows.

## Возможности

- **Источники данных:**
  - SQL Server (полные и дифференциальные бекапы)
  - 1С:Предприятие (файловые и SQL базы)
  - Файлы и документы (с фильтрами)

- **Хранилища:**
  - FTP/FTPS/SFTP серверы
  - Сетевые папки (UNC/SMB)

- **Функции:**
  - Гибкое расписание (ежедневно, еженедельно, ежемесячно, интервал)
  - Архивирование с компрессией (ZIP, 7Z)
  - Политика хранения (ротация бекапов)
  - Работа в режиме службы Windows
  - Уведомления по email

---

## Требования

### Минимальные
- **ОС:** Windows 10 / Windows Server 2016
- **.NET:** .NET 8.0 Runtime
- **RAM:** 512 MB
- **Disk:** 100 MB свободного места

### Для источников
- **SQL Server:** Microsoft.Data.SqlClient (включено)
- **1С:** Платформа 1С:Предприятие 8.3+ (опционально, для выгрузки DT)

---

## Установка

### 1. Установка .NET 8.0 Runtime

Скачайте и установите .NET 8.0 Runtime:
- https://dotnet.microsoft.com/download/dotnet/8.0/runtime

Или через winget:
```powershell
winget install Microsoft.DotNet.Runtime.8
```

### 2. Установка приложения

#### Вариант А: Из исходного кода

```powershell
# Перейдите в директорию проекта
cd BackupSystem

# Соберите проект
dotnet build -c Release

# Опубликуйте
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish
```

#### Вариант Б: Готовый дистрибутив

Скопируйте файлы из папки `publish` в целевую директорию:
```
C:\Program Files\BackupSystem\
```

### 3. Создание директорий

```powershell
# Данные приложения
mkdir "C:\ProgramData\BackupSystem"

# Логи
mkdir "C:\ProgramData\BackupSystem\Logs"

# Временные файлы
mkdir "C:\ProgramData\BackupSystem\Temp"
```

### 4. Настройка конфигурации

Скопируйте пример конфигурации:

```powershell
copy config\backup.example.json "C:\ProgramData\BackupSystem\backup.json"
```

Отредактируйте `C:\ProgramData\BackupSystem\backup.json` под ваши нужды.

---

## Настройка

### Пример: Бекап SQL Server

```json
{
  "id": "sql-main",
  "type": "sqlserver",
  "name": "Основная БД",
  "enabled": true,
  "settings": {
    "server": "localhost\\SQLEXPRESS",
    "database": "MyDatabase",
    "useIntegratedSecurity": "true",
    "backupType": "full"
  }
}
```

### Пример: Бекап 1С (файловая база)

```json
{
  "id": "1c-trade",
  "type": "ones",
  "name": "1С:Торговля",
  "enabled": true,
  "settings": {
    "platformPath": "C:\\Program Files\\1cv8\\current",
    "ibPath": "\\\\server\\1c\\trade",
    "dbType": "file"
  }
}
```

### Пример: Бекап файлов

```json
{
  "id": "files-docs",
  "type": "files",
  "name": "Документы",
  "enabled": true,
  "settings": {
    "paths": ["C:\\Users\\Public\\Documents", "D:\\Contracts"],
    "includeFilters": ["*.docx", "*.xlsx", "*.pdf"],
    "excludeFilters": ["*.tmp", "~$*"],
    "includeSubfolders": "true"
  }
}
```

### Пример: FTP хранилище

```json
{
  "id": "ftp-backup",
  "type": "ftp",
  "name": "Резервный FTP",
  "enabled": true,
  "settings": {
    "host": "backup.example.com",
    "port": "21",
    "protocol": "ftps",
    "username": "backup_user",
    "password": "your_password",
    "remotePath": "/backups",
    "passiveMode": "true"
  }
}
```

### Пример: Сетевое хранилище

```json
{
  "id": "nas-local",
  "type": "network",
  "name": "Локальный NAS",
  "enabled": true,
  "settings": {
    "uncPath": "\\\\NAS\\Backup",
    "username": "backup",
    "password": "your_password"
  }
}
```

### Пример: Задача бекапа

```json
{
  "id": "job-daily",
  "name": "Ежедневный бекап",
  "enabled": true,
  "sourceIds": ["sql-main", "1c-trade", "files-docs"],
  "destinationIds": ["nas-local", "ftp-backup"],
  "schedule": {
    "type": "daily",
    "time": "23:00"
  },
  "archiver": {
    "enabled": true,
    "format": "7z",
    "compressionLevel": "normal",
    "splitSize": 4294967296
  },
  "retention": {
    "keepDaily": 7,
    "keepWeekly": 4,
    "keepMonthly": 12
  }
}
```

---

## Установка службы Windows

### Через sc.exe

```powershell
# Создание службы
sc.exe create BackupSystem binPath= "C:\Program Files\BackupSystem\BackupSystem.Service.exe" start= auto DisplayName= "Система резервного копирования"

# Запуск службы
sc.exe start BackupSystem

# Проверка статуса
sc.exe query BackupSystem
```

### Через PowerShell

```powershell
# Создание службы
New-Service -Name "BackupSystem" `
  -BinaryPathName "C:\Program Files\BackupSystem\BackupSystem.Service.exe" `
  -DisplayName "Система резервного копирования" `
  -StartupType Automatic `
  -Description "Автоматическое резервное копирование данных"

# Запуск
Start-Service BackupSystem

# Статус
Get-Service BackupSystem
```

### Удаление службы

```powershell
# Остановка
Stop-Service BackupSystem

# Удаление
sc.exe delete BackupSystem
```

---

## Запуск GUI приложения

```powershell
# Из установленной директории
"C:\Program Files\BackupSystem\BackupSystem.UI.exe"

# Или из исходников
dotnet run --project src/UI/BackupSystem.UI.csproj
```

---

## Шифрование паролей

Для хранения паролей в конфигурации используйте DPAPI:

```powershell
# Шифрование
$plain = "my_password"
$secure = ConvertTo-SecureString $plain -AsPlainText -Force
$encrypted = ConvertFrom-SecureString $secure
Write-Host "${ENCRYPTED:$encrypted}"

# Вставьте результат в конфигурацию
```

---

## Логи

Логи находятся в:
```
C:\ProgramData\BackupSystem\Logs\
```

Формат логов:
```
[YYYY-MM-DD HH:MM:SS] [LEVEL] [Component] Message
```

---

## Структура проекта

```
BackupSystem/
├── src/
│   ├── Core/              # Ядро системы
│   ├── Sources/           # Источники (SQL, 1С, файлы)
│   ├── Destinations/      # Хранилища (FTP, сеть)
│   ├── Archiver/          # Архивирование
│   ├── Scheduler/         # Планировщик
│   ├── Service/           # Служба Windows
│   └── UI/                # GUI приложение (WPF)
├── config/
│   └── backup.example.json
├── BackupSystem.sln
└── README.md
```

---

## Сборка

```powershell
# Сборка решения
dotnet build BackupSystem.sln

# Публикация
dotnet publish -c Release -r win-x64 --self-contained false

# Создание MSI (опционально)
# Используйте WiX Toolset или Inno Setup
```

---

## Устранение неполадок

### Служба не запускается

1. Проверьте логи в `C:\ProgramData\BackupSystem\Logs`
2. Убедитесь, что .NET 8.0 установлен
3. Проверьте права доступа к конфигурации

### Ошибка подключения к SQL Server

1. Проверьте строку подключения
2. Убедитесь, что SQL Server доступен
3. Проверьте права пользователя

### Ошибка доступа к сетевой папке

1. Проверьте UNC путь (должен начинаться с `\\`)
2. Убедитесь, что учётные данные верны
3. Проверьте доступность сетевого ресурса

### 1С не выгружает базу

1. Убедитесь, что платформа 1С установлена
2. Проверьте путь к платформе (`platformPath`)
3. Для SQL баз 1С проверьте доступность SQL Server

---

## Поддержка

- Документация: `docs/`
- Примеры конфигураций: `config/`
- Исходный код: `src/`

---

## Лицензия

MIT License
