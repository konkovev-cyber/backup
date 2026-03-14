namespace BackupSystem.Core;

/// <summary>
/// Статус выполнения задачи бекапа
/// </summary>
public enum BackupJobStatus
{
    Pending,      // Ожидает запуска
    Running,      // Выполняется
    Paused,       // Приостановлен
    Completed,    // Успешно завершён
    Failed,       // Завершён с ошибкой
    Cancelled     // Отменён пользователем
}

/// <summary>
/// Представляет задачу резервного копирования
/// </summary>
public interface IBackupJob
{
    /// <summary>
    /// Уникальный идентификатор задачи
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Имя задачи
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Источники для бекапа
    /// </summary>
    IEnumerable<IBackupSource> Sources { get; }
    
    /// <summary>
    /// Целевые хранилища
    /// </summary>
    IEnumerable<IBackupDestination> Destinations { get; }
    
    /// <summary>
    /// Настройки архивирования
    /// </summary>
    ArchiverSettings? ArchiverSettings { get; }
    
    /// <summary>
    /// Настройки хранения (retention policy)
    /// </summary>
    RetentionSettings RetentionSettings { get; }
    
    /// <summary>
    /// Текущий статус
    /// </summary>
    BackupJobStatus Status { get; }
    
    /// <summary>
    /// Прогресс выполнения (0-100)
    /// </summary>
    double Progress { get; }
    
    /// <summary>
    /// Последнее сообщение о состоянии
    /// </summary>
    string? LastMessage { get; }
    
    /// <summary>
    /// Последнее время успешного выполнения
    /// </summary>
    DateTime? LastSuccessfulRun { get; }
    
    /// <summary>
    /// Запуск задачи
    /// </summary>
    Task<BackupResult> RunAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Отмена выполнения
    /// </summary>
    void Cancel();
}

/// <summary>
/// Результат выполнения задачи бекапа
/// </summary>
public class BackupResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public long TotalBytesProcessed { get; set; }
    public List<string> CreatedFiles { get; init; } = new();
    public List<string> DeletedFiles { get; init; } = new();
}
