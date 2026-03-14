using BackupSystem.Core;
using FluentFTP;
using Microsoft.Extensions.Logging;

namespace BackupSystem.Destinations;

/// <summary>
/// Хранилище FTP/FTPS/SFTP
/// </summary>
public class FtpDestination : IBackupDestination
{
    private readonly DestinationConfig _config;
    private readonly ILogger<FtpDestination>? _logger;
    private readonly AsyncFtpClient _ftpClient;
    
    public string Id => _config.Id;
    public string Name => _config.Name;
    public string Type => "ftp";
    
    public FtpDestination(DestinationConfig config, ILogger<FtpDestination>? logger = null)
    {
        _config = config;
        _logger = logger;
        
        var host = _config.GetSetting("host", "");
        var portStr = _config.GetSetting("port", "21");
        var protocol = _config.GetSetting("protocol", "ftp").ToLower();
        var username = _config.GetSetting("username", "");
        var password = _config.GetSetting("password", "");
        var passiveMode = _config.GetSetting("passiveMode", "true").ToLower() == "true";
        
        if (!int.TryParse(portStr, out int port)) port = 21;
        _ftpClient = new AsyncFtpClient(host, username, password, port);
        
        // Настройка протокола
        _ftpClient.Config.EncryptionMode = protocol switch
        {
            "ftps" => FtpEncryptionMode.Explicit,
            "ftps-implicit" => FtpEncryptionMode.Implicit,
            _ => FtpEncryptionMode.None
        };
        
        _ftpClient.Config.ValidateAnyCertificate = true; // TODO: Настроить валидацию
        _ftpClient.Config.DataConnectionType = passiveMode ? FtpDataConnectionType.PASV : FtpDataConnectionType.PORT;
        
        // SFTP
        if (protocol == "sftp")
        {
            _logger?.LogWarning("SFTP protocol requires separate client library");
        }
    }
    
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _ftpClient.Connect(cancellationToken);
            var isConnected = _ftpClient.IsConnected;
            await _ftpClient.Disconnect(cancellationToken);
            
            _logger?.LogDebug("FTP connection test: {Host} - {Status}", 
                _config.GetSetting("host", ""), 
                isConnected ? "OK" : "Failed");
            
            return isConnected;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "FTP availability check failed");
            return false;
        }
    }
    
    public async Task UploadAsync(string sourcePath, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _ftpClient.Connect(cancellationToken);
            
            // Создание директории если не существует
            var remoteDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(remoteDir))
            {
                await EnsureDirectoryExistsAsync(remoteDir.Replace("\\", "/"), cancellationToken);
            }
            
            // Загрузка файла
            var ftpProgress = new Progress<FtpProgress>(p =>
            {
                if (p.Progress >= 0)
                {
                    progress?.Report(p.Progress);
                }
            });
            
            await _ftpClient.UploadFile(
                sourcePath, 
                destinationPath.Replace("\\", "/"), 
                FtpRemoteExists.Overwrite, 
                true, 
                FtpVerify.Retry,
                ftpProgress,
                cancellationToken);
            
            _logger?.LogInformation("FTP upload completed: {Source} -> {Destination}", 
                sourcePath, destinationPath);
        }
        finally
        {
            if (_ftpClient.IsConnected)
            {
                await _ftpClient.Disconnect(cancellationToken);
            }
        }
    }
    
    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await _ftpClient.Connect(cancellationToken);
            
            if (await _ftpClient.FileExists(path.Replace("\\", "/"), cancellationToken))
            {
                await _ftpClient.DeleteFile(path.Replace("\\", "/"), cancellationToken);
                _logger?.LogDebug("FTP file deleted: {Path}", path);
            }
        }
        finally
        {
            if (_ftpClient.IsConnected)
            {
                await _ftpClient.Disconnect(cancellationToken);
            }
        }
    }
    
    public async Task<IEnumerable<string>> ListFilesAsync(string path, CancellationToken cancellationToken = default)
    {
        var files = new List<string>();
        
        try
        {
            await _ftpClient.Connect(cancellationToken);
            
            var items = await _ftpClient.GetListing(path.Replace("\\", "/"), cancellationToken);
            
            foreach (var item in items)
            {
                if (item.Type == FtpObjectType.File)
                {
                    files.Add(item.FullName);
                }
            }
        }
        finally
        {
            if (_ftpClient.IsConnected)
            {
                await _ftpClient.Disconnect(cancellationToken);
            }
        }
        
        return files;
    }
    
    public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await _ftpClient.Connect(cancellationToken);
            return await _ftpClient.FileExists(path.Replace("\\", "/"), cancellationToken);
        }
        finally
        {
            if (_ftpClient.IsConnected)
            {
                await _ftpClient.Disconnect(cancellationToken);
            }
        }
    }
    
    private async Task EnsureDirectoryExistsAsync(string path, CancellationToken cancellationToken)
    {
        path = path.Replace("\\", "/");
        if (await _ftpClient.DirectoryExists(path, cancellationToken))
        {
            return;
        }
        
        // Рекурсивное создание директорий
        var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        if (!string.IsNullOrEmpty(parent) && parent != "/" && parent != ".")
        {
            await EnsureDirectoryExistsAsync(parent, cancellationToken);
        }
        
        await _ftpClient.CreateDirectory(path, cancellationToken);
        _logger?.LogDebug("FTP directory created: {Path}", path);
    }
    
    public void Dispose()
    {
        _ftpClient?.Dispose();
    }
}

/// <summary>
/// Фабрика для создания FTP хранилищ
/// </summary>
public class FtpDestinationFactory
{
    private readonly ILogger<FtpDestination>? _logger;
    
    public FtpDestinationFactory(ILogger<FtpDestination>? logger = null)
    {
        _logger = logger;
    }
    
    public IBackupDestination Create(DestinationConfig config)
    {
        return new FtpDestination(config, _logger);
    }
}
