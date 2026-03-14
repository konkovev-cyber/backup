using BackupSystem.Core;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace BackupSystem.Sources;

/// <summary>
/// Источник данных - файлы и папки
/// </summary>
public class FileSource : IBackupSource
{
    private readonly SourceConfig _config;
    private readonly ILogger<FileSource>? _logger;
    private readonly List<string> _paths;
    private readonly List<string> _includeFilters;
    private readonly List<string> _excludeFilters;
    private readonly bool _includeSubfolders;
    private readonly bool _followJunctions;
    
    public string Id => _config.Id;
    public string Name => _config.Name;
    public string Type => "files";
    
    public FileSource(SourceConfig config, ILogger<FileSource>? logger = null)
    {
        _config = config;
        _logger = logger;
        
        // Парсинг путей
        _paths = _config.GetSetting("paths").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        
        // Фильтры включения
        var includeFiltersStr = _config.GetSetting("includeFilters");
        _includeFilters = string.IsNullOrEmpty(includeFiltersStr) 
            ? new List<string> { "*.*" } 
            : includeFiltersStr.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        
        // Фильтры исключения
        _excludeFilters = _config.GetSetting("excludeFilters").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        
        _includeSubfolders = _config.GetSetting("includeSubfolders", "true").ToLower() == "true";
        _followJunctions = _config.GetSetting("followJunctions", "false").ToLower() == "true";
    }
    
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        foreach (var path in _paths)
        {
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                _logger?.LogWarning("Path not available: {Path}", path);
                return false;
            }
        }
        
        return true;
    }
    
    public async Task<string> BackupAsync(string outputPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting file backup for {Name}", Name);
        _logger?.LogDebug("Output: {OutputPath}", outputPath);
        
        progress?.Report(0);
        
        // Сбор списка файлов
        var filesToBackup = await CollectFilesAsync(cancellationToken);
        
        if (filesToBackup.Count == 0)
        {
            _logger?.LogWarning("No files found matching criteria");
            // Создаём пустой архив
            ZipFile.CreateFromDirectory(Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName, outputPath);
            return outputPath;
        }
        
        _logger?.LogInformation("Found {Count} files to backup", filesToBackup.Count);
        
        // Создание ZIP архива
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        
        var totalFiles = filesToBackup.Count;
        var processedFiles = 0;
        var totalBytes = 0L;
        
        foreach (var file in filesToBackup)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                var fileInfo = new FileInfo(file);
                
                // Определение пути внутри архива
                var entryName = file;
                foreach (var path in _paths)
                {
                    if (file.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                    {
                        entryName = Path.GetRelativePath(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!, file);
                        break;
                    }
                }

                // Создание записи в архиве
                var entry = archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                
                totalBytes += fileInfo.Length;
                processedFiles++;
                
                // Прогресс (0-100%)
                progress?.Report((double)processedFiles / totalFiles * 100);
                
                _logger?.LogDebug("Backed up: {File} ({Size} bytes)", file, fileInfo.Length);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to backup file: {File}", file);
            }
        }
        
        progress?.Report(100);
        
        _logger?.LogInformation("File backup completed: {Count} files, {Bytes} bytes", 
            processedFiles, totalBytes);
        
        return outputPath;
    }
    
    public async Task<long> GetEstimatedSizeAsync(CancellationToken cancellationToken = default)
    {
        var files = await CollectFilesAsync(cancellationToken);
        
        long totalSize = 0;
        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                totalSize += fileInfo.Length;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cannot get size of file: {File}", file);
            }
        }
        
        return totalSize;
    }
    
    private async Task<List<string>> CollectFilesAsync(CancellationToken cancellationToken)
    {
        var files = new List<string>();
        
        foreach (var path in _paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (File.Exists(path))
            {
                if (ShouldInclude(path))
                {
                    files.Add(path);
                }
            }
            else if (Directory.Exists(path))
            {
                var searchOption = _includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                
                foreach (var filter in _includeFilters)
                {
                    try
                    {
                        var foundFiles = Directory.GetFiles(path, filter, searchOption);
                        
                        foreach (var file in foundFiles)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            if (ShouldInclude(file) && !ShouldExclude(file))
                            {
                                // Проверка на symlink/junction
                                if (!_followJunctions && IsJunction(file))
                                {
                                    _logger?.LogDebug("Skipping junction: {File}", file);
                                    continue;
                                }
                                
                                files.Add(file);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error collecting files with filter {Filter} in {Path}", filter, path);
                    }
                }
            }
        }
        
        return files;
    }
    
    private bool ShouldInclude(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        // Если нет фильтров включения - включаем всё
        if (_includeFilters.Count == 0 || _includeFilters.Contains("*.*"))
        {
            return true;
        }
        
        foreach (var filter in _includeFilters)
        {
            if (MatchesFilter(fileName, filter))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private bool ShouldExclude(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        foreach (var filter in _excludeFilters)
        {
            if (MatchesFilter(fileName, filter))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private bool MatchesFilter(string fileName, string filter)
    {
        // Преобразование wildcard паттерна в regex
        var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(filter)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        
        return System.Text.RegularExpressions.Regex.IsMatch(fileName, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
    
    private bool IsJunction(string path)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            return (fileInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Фабрика для создания файловых источников
/// </summary>
public class FileSourceFactory
{
    private readonly ILogger<FileSource>? _logger;
    
    public FileSourceFactory(ILogger<FileSource>? logger = null)
    {
        _logger = logger;
    }
    
    public IBackupSource Create(SourceConfig config)
    {
        return new FileSource(config, _logger);
    }
}
