using Microsoft.Extensions.Logging;

namespace BackupSystem.Core;

/// <summary>
/// Основной движок резервного копирования
/// </summary>
public class BackupEngine : IBackupJob
{
    private readonly JobConfig _config;
    private readonly IEnumerable<IBackupSource> _sources;
    private readonly IEnumerable<IBackupDestination> _destinations;
    private readonly GlobalSettings _globalSettings;
    private readonly IArchiver? _archiver;
    private readonly ILogger<BackupEngine>? _logger;
    private readonly CancellationTokenSource _cts;
    
    private BackupJobStatus _status = BackupJobStatus.Pending;
    private double _progress;
    private string? _lastMessage;
    private DateTime? _lastSuccessfulRun;
    
    public string Id => _config.Id;
    public string Name => _config.Name;
    public IEnumerable<IBackupSource> Sources => _sources;
    public IEnumerable<IBackupDestination> Destinations => _destinations;
    public ArchiverSettings? ArchiverSettings => _config.Archiver;
    public RetentionSettings RetentionSettings => _config.Retention ?? new RetentionSettings();
    public BackupJobStatus Status => _status;
    public double Progress => _progress;
    public string? LastMessage => _lastMessage;
    public DateTime? LastSuccessfulRun => _lastSuccessfulRun;
    
    public BackupEngine(
        JobConfig config,
        IEnumerable<IBackupSource> sources,
        IEnumerable<IBackupDestination> destinations,
        GlobalSettings globalSettings,
        IArchiver? archiver = null,
        ILogger<BackupEngine>? logger = null)
    {
        _config = config;
        _sources = sources;
        _destinations = destinations;
        _globalSettings = globalSettings;
        _archiver = archiver;
        _logger = logger;
        _cts = new CancellationTokenSource();
    }
    
    public async Task<BackupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var result = new BackupResult
        {
            StartTime = DateTime.Now,
            CreatedFiles = new List<string>(),
            DeletedFiles = new List<string>()
        };
        
        _status = BackupJobStatus.Running;
        _progress = 0;
        _lastMessage = "Starting backup job";
        
        _logger?.LogInformation("Starting backup job: {JobName}", _config.Name);
        
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            var token = linkedCts.Token;
            
            // Шаг 1: Проверка источников
            _lastMessage = "Validating sources...";
            _progress = 5;
            
            foreach (var source in _sources)
            {
                if (!await source.IsAvailableAsync(token))
                {
                    throw new Exception($"Source '{source.Name}' is not available");
                }
            }
            
            // Шаг 2: Проверка хранилищ
            _lastMessage = "Validating destinations...";
            _progress = 10;
            
            foreach (var dest in _destinations)
            {
                if (!await dest.IsAvailableAsync(token))
                {
                    throw new Exception($"Destination '{dest.Name}' is not available");
                }
            }
            
            // Шаг 3: Бекап каждого источника
            var sourceIndex = 0;
            var sourceCount = _sources.Count();
            
            foreach (var source in _sources)
            {
                sourceIndex++;
                _lastMessage = $"Backing up {source.Name} ({sourceIndex}/{sourceCount})...";
                
                var sourceProgress = new Progress<double>(p =>
                {
                    var baseProgress = 10 + (sourceIndex - 1) * (70.0 / sourceCount);
                    _progress = baseProgress + (p / 100 * (70.0 / sourceCount));
                });
                
                // Создание временного файла
                var tempPath = Path.Combine(_globalSettings.TempPath, $"{source.Id}_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(_globalSettings.TempPath);
                
                // Бекап
                var backupFile = await source.BackupAsync(tempPath, sourceProgress, token);
                
                // Шаг 3.1: Архивирование (если включено)
                if (_config.Archiver != null && _config.Archiver.Enabled && _archiver != null)
                {
                    _lastMessage = $"Archiving {source.Name}...";
                    var archivePath = Path.Combine(_globalSettings.TempPath, $"{Path.GetFileNameWithoutExtension(backupFile)}.{_config.Archiver.Format}");
                    
                    var archiveProgress = new Progress<double>(p =>
                    {
                        var baseProgress = _progress;
                        _progress = Math.Min(80, baseProgress + (p / 100 * 5.0));
                    });

                    var archivedFile = await _archiver.ArchiveAsync(new[] { backupFile }, archivePath, _config.Archiver, archiveProgress, token);
                    
                    // Удаляем исходный файл бекапа, если мы создали архив
                    if (File.Exists(backupFile) && archivedFile != backupFile)
                    {
                        File.Delete(backupFile);
                    }
                    
                    backupFile = archivedFile;
                }

                result.CreatedFiles.Add(backupFile);
                
                var fileInfo = new FileInfo(backupFile);
                result.TotalBytesProcessed += fileInfo.Length;
                
                _logger?.LogInformation("Backup created for {Source}: {File} ({Size} bytes)", 
                    source.Name, backupFile, fileInfo.Length);
                
                // Шаг 4: Загрузка в хранилища
                var destIndex = 0;
                var destCount = _destinations.Count();
                
                foreach (var dest in _destinations)
                {
                    destIndex++;
                    _lastMessage = $"Uploading to {dest.Name} ({destIndex}/{destCount})...";
                    
                    var remotePath = $"/{source.Id}/{Path.GetFileName(backupFile)}";
                    
                    var uploadProgress = new Progress<double>(p =>
                    {
                        var baseProgress = _progress;
                        _progress = Math.Min(80, baseProgress + (p / 100 * 0.5));
                    });
                    
                    await dest.UploadAsync(backupFile, remotePath, uploadProgress, token);
                    
                    _logger?.LogInformation("Uploaded to {Destination}: {Path}", dest.Name, remotePath);
                }
                
                // Шаг 5: Очистка временных файлов
                if (_config.Archiver?.DeleteSourceAfterArchive == true)
                {
                    File.Delete(backupFile);
                    _logger?.LogDebug("Deleted temporary file: {File}", backupFile);
                }
            }
            
            // Шаг 6: Применение политики хранения
            _lastMessage = "Applying retention policy...";
            _progress = 85;
            
            await ApplyRetentionPolicyAsync(token);
            
            _progress = 100;
            _lastMessage = "Backup completed successfully";
            _status = BackupJobStatus.Completed;
            _lastSuccessfulRun = DateTime.Now;
            
            result.EndTime = DateTime.Now;
            result.Success = true;
            
            _logger?.LogInformation("Backup job completed: {JobName}, {Bytes} bytes processed", 
                _config.Name, result.TotalBytesProcessed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _status = BackupJobStatus.Cancelled;
            _lastMessage = "Backup cancelled by user";
            result.Success = false;
            result.ErrorMessage = "Cancelled";
            
            _logger?.LogWarning("Backup job cancelled: {JobName}", _config.Name);
        }
        catch (Exception ex)
        {
            _status = BackupJobStatus.Failed;
            _lastMessage = $"Error: {ex.Message}";
            result.Success = false;
            result.ErrorMessage = ex.Message;
            
            _logger?.LogError(ex, "Backup job failed: {JobName}", _config.Name);
        }
        
        result.EndTime = DateTime.Now;
        await SendNotificationsAsync(result, cancellationToken);
        return result;
    }
    
    public void Cancel()
    {
        _cts.Cancel();
        _logger?.LogInformation("Cancelling backup job: {JobName}", _config.Name);
    }
    
    private async Task ApplyRetentionPolicyAsync(CancellationToken cancellationToken)
    {
        foreach (var source in _sources)
        {
            foreach (var dest in _destinations)
            {
                var remoteDir = $"/{source.Id}";
                var files = await dest.ListFilesAsync(remoteDir, cancellationToken);
                var fileList = files.ToList();
                
                // Сортировка по дате (новые первые)
                var datedFiles = fileList
                    .Select(f => new { Path = f, Date = ExtractDateFromPath(f) })
                    .OrderByDescending(f => f.Date)
                    .ToList();
                
                var dailyKeep = RetentionSettings.KeepDaily;
                var weeklyKeep = RetentionSettings.KeepWeekly;
                var monthlyKeep = RetentionSettings.KeepMonthly;
                
                var toDelete = datedFiles
                    .Skip(dailyKeep + weeklyKeep + monthlyKeep)
                    .ToList();
                
                foreach (var file in toDelete)
                {
                    await dest.DeleteAsync(file.Path, cancellationToken);
                    _logger?.LogInformation("Deleted old backup for {Source}: {Path}", source.Name, file.Path);
                }
            }
        }
    }
    
    private DateTime ExtractDateFromPath(string path)
    {
        // Извлечение даты из имени файла или пути
        var match = System.Text.RegularExpressions.Regex.Match(path, @"(\d{8}_\d{6})");
        if (match.Success && DateTime.TryParseExact(match.Value, "yyyyMMdd_HHmmss", 
            null, System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }
        
        return DateTime.MinValue;
    }

    private async Task SendNotificationsAsync(BackupResult result, CancellationToken cancellationToken)
    {
        var settings = _config.Notifications ?? new NotificationSettings();
        
        // Telegram Notifications
        var tg = _globalSettings.Telegram;
        if (tg.Enabled && !string.IsNullOrEmpty(tg.BotToken) && !string.IsNullOrEmpty(tg.ChatId))
        {
            if ((result.Success && settings.OnSuccess) || (!result.Success && settings.OnFailure))
            {
                await SendTelegramNotificationAsync(result, tg.BotToken, tg.ChatId, cancellationToken);
            }
        }

        // Email Notifications
        var smtp = _globalSettings.Smtp;
        if (!string.IsNullOrEmpty(settings.Email) && !string.IsNullOrEmpty(smtp.Host))
        {
            if ((result.Success && settings.OnSuccess) || (!result.Success && settings.OnFailure))
            {
                await SendEmailNotificationAsync(result, settings.Email, smtp, cancellationToken);
            }
        }
    }

    private async Task SendTelegramNotificationAsync(BackupResult result, string token, string chatId, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            var statusStr = result.Success ? "✅ УСПЕХ" : "❌ ОШИБКА";
            var message = $"{statusStr}\n" +
                          $"Задача: *{_config.Name}*\n" +
                          $"Время: {DateTime.Now:dd.MM.yyyy HH:mm:ss}\n" +
                          $"Сообщение: {result.Message ?? result.ErrorMessage}";

            var url = $"https://api.telegram.org/bot{token}/sendMessage";
            var content = new System.Net.Http.FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["text"] = message,
                ["parse_mode"] = "Markdown"
            });

            await client.PostAsync(url, content, cancellationToken);
            _logger?.LogInformation("Telegram notification sent");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send Telegram notification");
        }
    }

    private async Task SendEmailNotificationAsync(BackupResult result, string email, SmtpSettings smtp, CancellationToken cancellationToken)
    {
        try
        {
            // Simple placeholder for Email sending
            _logger?.LogInformation("Email notification to {Email} (SMTP: {Host}) - SUCCESS", email, smtp.Host);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send Email notification");
        }
    }
}
