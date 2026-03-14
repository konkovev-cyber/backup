namespace BackupSystem.Core;

/// <summary>
/// Интерфейс для архивирования файлов
/// </summary>
public interface IArchiver
{
    /// <summary>
    /// Создать архив из файлов или папок
    /// </summary>
    /// <param name="sourcePaths">Список путей к файлам или папкам</param>
    /// <param name="destinationPath">Путь к создаваемому архиву</param>
    /// <param name="settings">Настройки архивирования</param>
    /// <param name="progress">Прогресс выполнения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Путь к созданному архиву</returns>
    Task<string> ArchiveAsync(
        IEnumerable<string> sourcePaths,
        string destinationPath,
        ArchiverSettings settings,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверить целостность архива
    /// </summary>
    Task<bool> VerifyAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default);
}
