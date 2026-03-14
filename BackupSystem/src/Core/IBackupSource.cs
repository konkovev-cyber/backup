namespace BackupSystem.Core;

/// <summary>
/// Представляет источник данных для резервного копирования
/// </summary>
public interface IBackupSource
{
    /// <summary>
    /// Уникальный идентификатор источника
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Человекочитаемое имя
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Тип источника (sqlserver, ones, files)
    /// </summary>
    string Type { get; }
    
    /// <summary>
    /// Проверка доступности источника
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Получение данных для бекапа
    /// </summary>
    /// <param name="outputPath">Путь для сохранения данных</param>
    /// <param name="progress">Прогресс выполнения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Путь к созданному файлу бекапа</returns>
    Task<string> BackupAsync(string outputPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Получение размера данных в байтах
    /// </summary>
    Task<long> GetEstimatedSizeAsync(CancellationToken cancellationToken = default);
}
