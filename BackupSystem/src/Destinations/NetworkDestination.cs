using BackupSystem.Core;
using Microsoft.Extensions.Logging;
using System.IO;

namespace BackupSystem.Destinations;

/// <summary>
/// Хранилище - сетевая папка (UNC/SMB)
/// </summary>
public class NetworkDestination : IBackupDestination
{
    private readonly DestinationConfig _config;
    private readonly ILogger<NetworkDestination>? _logger;
    private readonly string _uncPath;
    private readonly string? _username;
    private readonly string? _password;
    
    public string Id => _config.Id;
    public string Name => _config.Name;
    public string Type => "network";
    
    public NetworkDestination(DestinationConfig config, ILogger<NetworkDestination>? logger = null)
    {
        _config = config;
        _logger = logger;
        
        _uncPath = _config.GetSetting("uncPath", "");
        _username = _config.GetSetting("username", "");
        _password = _config.GetSetting("password", "");
        
        if (string.IsNullOrEmpty(_uncPath))
        {
            throw new ArgumentException("UNC path is required for network destination");
        }
        
        if (!_uncPath.StartsWith("\\\\"))
        {
            _logger?.LogWarning("UNC path should start with \\\\");
        }
    }
    
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Проверка доступности сетевого пути
            var directory = new DirectoryInfo(_uncPath);
            
            // Попытка получить доступ к директории
            directory.GetFiles("*");
            
            _logger?.LogDebug("Network share availability test: {Path} - OK", _uncPath);
            
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.LogError(ex, "Access denied to network share: {Path}", _uncPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Network share availability check failed: {Path}", _uncPath);
            return false;
        }
    }
    
    public async Task UploadAsync(string sourcePath, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        // Если destinationPath - это путь в сетевой папке
        var fullPath = Path.Combine(_uncPath, destinationPath.TrimStart('\\', '/'));
        
        _logger?.LogInformation("Copying file to network share: {Source} -> {Destination}", 
            sourcePath, fullPath);
        
        // Убедиться, что директория существует
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger?.LogDebug("Directory created: {Directory}", directory);
        }
        
        // Копирование файла
        await CopyFileWithProgressAsync(sourcePath, fullPath, progress, cancellationToken);
        
        _logger?.LogInformation("File copied to network share: {Path}", fullPath);
    }
    
    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_uncPath, path.TrimStart('\\', '/'));
        
        if (File.Exists(fullPath))
        {
            await Task.Run(() => File.Delete(fullPath), cancellationToken);
            _logger?.LogDebug("File deleted from network share: {Path}", fullPath);
        }
    }
    
    public async Task<IEnumerable<string>> ListFilesAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_uncPath, path.TrimStart('\\', '/'));
        
        return await Task.Run(() =>
        {
            if (!Directory.Exists(fullPath))
            {
                return Enumerable.Empty<string>();
            }
            
            return Directory.GetFiles(fullPath).Select(f => Path.GetFileName(f));
        }, cancellationToken);
    }
    
    public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_uncPath, path.TrimStart('\\', '/'));
        
        return await Task.Run(() => File.Exists(fullPath), cancellationToken);
    }
    
    private async Task CopyFileWithProgressAsync(string source, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        const int bufferSize = 81920; // 80 KB
        
        var sourceInfo = new FileInfo(source);
        var totalBytes = sourceInfo.Length;
        var bytesCopied = 0L;
        
        using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous);
        using var destinationStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous);
        
        var buffer = new byte[bufferSize];
        int bytesRead;
        
        while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await destinationStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            bytesCopied += bytesRead;
            
            var percent = (double)bytesCopied / totalBytes * 100;
            progress?.Report(percent);
        }
        
        await destinationStream.FlushAsync(cancellationToken);
    }
}

/// <summary>
/// Фабрика для создания сетевых хранилищ
/// </summary>
public class NetworkDestinationFactory
{
    private readonly ILogger<NetworkDestination>? _logger;
    
    public NetworkDestinationFactory(ILogger<NetworkDestination>? logger = null)
    {
        _logger = logger;
    }
    
    public IBackupDestination Create(DestinationConfig config)
    {
        return new NetworkDestination(config, _logger);
    }
}
