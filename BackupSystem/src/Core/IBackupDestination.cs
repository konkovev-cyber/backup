namespace BackupSystem.Core;

/// <summary>
/// Представляет целевое хранилище для резервных копий
/// </summary>
public interface IBackupDestination
{
    /// <summary>
    /// Уникальный идентификатор хранилища
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Человекочитаемое имя
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Тип хранилища (ftp, network)
    /// </summary>
    string Type { get; }
    
    /// <summary>
    /// Проверка доступности хранилища
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Загрузка файла в хранилище
    /// </summary>
    /// <param name="sourcePath">Путь к исходному файлу</param>
    /// <param name="destinationPath">Путь в хранилище</param>
    /// <param name="progress">Прогресс загрузки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task UploadAsync(string sourcePath, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Удаление файла из хранилища
    /// </summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Получение списка файлов
    /// </summary>
    Task<IEnumerable<string>> ListFilesAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Проверка существования файла
    /// </summary>
    Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);
}
