using BackupSystem.Core;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Compression;

namespace BackupSystem.Sources;

/// <summary>
/// Источник данных 1С:Предприятие
/// Поддерживает файловые и SQL базы
/// </summary>
public class OneSSource : IBackupSource
{
    private readonly SourceConfig _config;
    private readonly ILogger<OneSSource>? _logger;
    
    public string Id => _config.Id;
    public string Name => _config.Name;
    public string Type => "ones";
    
    public OneSSource(SourceConfig config, ILogger<OneSSource>? logger = null)
    {
        _config = config;
        _logger = logger;
    }
    
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dbType = _config.GetSetting("dbType", "file");
            
            if (dbType == "file")
            {
                var ibPath = _config.GetSetting("ibPath", "");
                return Directory.Exists(ibPath);
            }
            else
            {
                // Для SQL баз проверяем доступность SQL Server
                var dbServer = _config.GetSetting("dbServer", "");
                var dbName = _config.GetSetting("dbName", "");
                
                // Простая проверка доступности сервера
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(dbServer, 3000);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "1C source availability check failed");
            return false;
        }
    }
    
    public async Task<string> BackupAsync(string outputPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var platformPath = _config.GetSetting("platformPath", @"C:\Program Files\1cv8\current");
        var ibPath = _config.GetSetting("ibPath", "");
        var dbType = _config.GetSetting("dbType", "file");
        
        _logger?.LogInformation("Starting 1C backup for {Name}, type: {DbType}", Name, dbType);
        
        progress?.Report(0);
        
        if (dbType == "file")
        {
            // Файловая база - копируем каталог
            return await BackupFileBaseAsync(ibPath, outputPath, progress, cancellationToken);
        }
        else
        {
            // SQL база - используем 1С для создания DT файла или бекап SQL
            return await BackupSqlBaseAsync(platformPath, ibPath, outputPath, progress, cancellationToken);
        }
    }
    
    public async Task<long> GetEstimatedSizeAsync(CancellationToken cancellationToken = default)
    {
        var dbType = _config.GetSetting("dbType", "file");
        
        if (dbType == "file")
        {
            var ibPath = _config.GetSetting("ibPath", "");
            if (Directory.Exists(ibPath))
            {
                return GetDirectorySize(ibPath);
            }
        }
        else
        {
            // Для SQL баз - оценка через SqlServerSource
            var dbServer = _config.GetSetting("dbServer", "");
            var dbName = _config.GetSetting("dbName", "");
            var dbUser = _config.GetSetting("dbUser", "");
            var dbPassword = _config.GetSetting("dbPassword", "");

            if (string.IsNullOrEmpty(dbServer) || string.IsNullOrEmpty(dbName))
            {
                return 0;
            }

            var sqlSource = new SqlServerSource(new SourceConfig
            {
                Id = _config.Id,
                Name = _config.Name,
                Type = "sqlserver",
                Settings = new Dictionary<string, object>
                {
                    ["server"] = dbServer,
                    ["database"] = dbName,
                    ["useIntegratedSecurity"] = string.IsNullOrEmpty(dbUser) ? "true" : "false",
                    ["username"] = dbUser,
                    ["password"] = dbPassword
                }
            }, null);
            
            try 
            {
                return await sqlSource.GetEstimatedSizeAsync(cancellationToken);
            }
            catch 
            {
                return 0;
            }
        }
        
        return 0;
    }
    
    private async Task<string> BackupFileBaseAsync(string sourcePath, string outputPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Backing up file 1C base from {SourcePath}", sourcePath);
        
        // Создаём временную копию для консистентности
        var tempPath = Path.Combine(Path.GetTempPath(), $"1c_backup_{Guid.NewGuid()}");
        
        try
        {
            // Копирование файлов базы
            await CopyDirectoryAsync(sourcePath, tempPath, progress, cancellationToken);
            
            // Архивирование
            progress?.Report(50);
            
            // Создаём DT файл через 1С (если доступна) или просто копируем
            var platformPath = _config.GetSetting("platformPath", @"C:\Program Files\1cv8\current");
            var v8Runner = Path.Combine(platformPath, "v8.exe");
            
            if (File.Exists(v8Runner))
            {
                // Попытка выгрузить через 1С
                var dtFile = Path.Combine(tempPath, "backup.dt");
                var result = await Run1CDumpAsync(v8Runner, tempPath, dtFile, cancellationToken);
                
                if (result && File.Exists(dtFile))
                {
                    // Копируем DT файл в результат
                    File.Copy(dtFile, outputPath, true);
                    progress?.Report(100);
                    return outputPath;
                }
            }
            
            // Если 1С недоступна - архивируем каталог
            using var archive = System.IO.Compression.ZipFile.Open(outputPath, System.IO.Compression.ZipArchiveMode.Create);
            
            foreach (var file in Directory.GetFiles(tempPath, "*.*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var relativePath = Path.GetRelativePath(tempPath, file);
                archive.CreateEntryFromFile(file, relativePath);
            }
            
            progress?.Report(100);
            return outputPath;
        }
        finally
        {
            // Очистка временных файлов
            if (Directory.Exists(tempPath))
            {
                try { Directory.Delete(tempPath, true); }
                catch { /* Игнорируем ошибки очистки */ }
            }
        }
    }
    
    private async Task<string> BackupSqlBaseAsync(string platformPath, string ibPath, string outputPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Backing up SQL 1C base: {DbPath}", ibPath);
        
        var dbServer = _config.GetSetting("dbServer", "");
        var dbName = _config.GetSetting("dbName", "");
        var dbUser = _config.GetSetting("dbUser", "");
        var dbPassword = _config.GetSetting("dbPassword", "");
        
        // Попытка выгрузки через 1С
        var v8Runner = Path.Combine(platformPath, "v8.exe");
        
        if (File.Exists(v8Runner))
        {
            var result = await Run1CBackupAsync(v8Runner, ibPath, outputPath, cancellationToken);
            if (result)
            {
                progress?.Report(100);
                return outputPath;
            }
        }
        
        // Если 1С недоступна - создаём SQL бекап
        _logger?.LogWarning("1C platform not available, falling back to SQL backup");
        
        // Создаём SQL бекап (упрощённо)
        var sqlSource = new SqlServerSource(new SourceConfig
        {
            Id = _config.Id,
            Name = _config.Name,
            Type = "sqlserver",
            Settings = new Dictionary<string, object>
            {
                ["server"] = dbServer,
                ["database"] = dbName,
                ["useIntegratedSecurity"] = string.IsNullOrEmpty(dbUser) ? "true" : "false",
                ["username"] = dbUser,
                ["password"] = dbPassword,
                ["backupType"] = "full"
            }
        }, null);
        
        return await sqlSource.BackupAsync(outputPath, progress, cancellationToken);
    }
    
    private async Task<bool> Run1CDumpAsync(string v8Exe, string ibPath, string dtFile, CancellationToken cancellationToken)
    {
        var user = _config.GetSetting("onesUser", "admin");
        var pass = _config.GetSetting("onesPassword", "");
        var auth = string.IsNullOrEmpty(pass) ? $"/N {user}" : $"/N {user} /P {pass}";

        var startInfo = new ProcessStartInfo
        {
            FileName = v8Exe,
            Arguments = $@"DESIGNER /S ""{ibPath}"" /DumpIB ""{dtFile}"" /DisableStartupMessages {auth}",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        _logger?.LogDebug("Running 1C: {FileName} {Arguments}", startInfo.FileName, startInfo.Arguments);
        
        using var process = Process.Start(startInfo);
        if (process == null) return false;
        
        await process.WaitForExitAsync(cancellationToken);
        
        var success = process.ExitCode == 0;
        _logger?.LogInformation("1C dump completed with exit code: {ExitCode}", process.ExitCode);
        
        return success;
    }
    
    private async Task<bool> Run1CBackupAsync(string v8Exe, string ibPath, string outputFile, CancellationToken cancellationToken)
    {
        var user = _config.GetSetting("onesUser", "admin");
        var pass = _config.GetSetting("onesPassword", "");
        var auth = string.IsNullOrEmpty(pass) ? $"/N {user}" : $"/N {user} /P {pass}";

        var startInfo = new ProcessStartInfo
        {
            FileName = v8Exe,
            Arguments = $@"DESIGNER /S ""{ibPath}"" /DumpIB ""{outputFile}"" /DisableStartupMessages {auth}",
            CreateNoWindow = true,
            UseShellExecute = false
        };
        
        _logger?.LogDebug("Running 1C backup: {FileName} {Arguments}", startInfo.FileName, startInfo.Arguments);
        
        using var process = Process.Start(startInfo);
        if (process == null) return false;
        
        await process.WaitForExitAsync(cancellationToken);
        
        return process.ExitCode == 0;
    }
    
    private async Task CopyDirectoryAsync(string source, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        
        var files = Directory.GetFiles(source, "*.*", SearchOption.AllDirectories);
        var totalFiles = files.Length;
        var copiedFiles = 0;
        
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var relativePath = Path.GetRelativePath(source, file);
            var destFile = Path.Combine(destination, relativePath);
            
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, true);
            
            copiedFiles++;
            progress?.Report((double)copiedFiles / totalFiles * 50); // 0-50%
        }
    }
    
    private long GetDirectorySize(string path)
    {
        long size = 0;
        
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            {
                var fileInfo = new FileInfo(file);
                size += fileInfo.Length;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error calculating directory size for {Path}", path);
        }
        
        return size;
    }
}

/// <summary>
/// Фабрика для создания источников 1С
/// </summary>
public class OneSSourceFactory
{
    private readonly ILogger<OneSSource>? _logger;
    
    public OneSSourceFactory(ILogger<OneSSource>? logger = null)
    {
        _logger = logger;
    }
    
    public IBackupSource Create(SourceConfig config)
    {
        return new OneSSource(config, _logger);
    }
}
