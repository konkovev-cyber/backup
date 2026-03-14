using BackupSystem.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IO.Compression; // Added based on the provided Code Edit block

namespace BackupSystem.Sources;

/// <summary>
/// Источник данных SQL Server
/// </summary>
public class SqlServerSource : IBackupSource
{
    private readonly SourceConfig _config;
    private readonly ILogger<SqlServerSource>? _logger;
    private readonly string _connectionString;
    
    public string Id => _config.Id;
    public string Name => _config.Name;
    public string Type => "sqlserver";
    
    public SqlServerSource(SourceConfig config, ILogger<SqlServerSource>? logger = null)
    {
        _config = config;
        _logger = logger;
        
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _config.GetSetting("server", "localhost"),
            InitialCatalog = _config.GetSetting("database", ""),
            IntegratedSecurity = _config.GetSetting("useIntegratedSecurity", "true").ToLower() == "true"
        };
        
        if (!builder.IntegratedSecurity)
        {
            builder.UserID = _config.GetSetting("username", "");
            builder.Password = _config.GetSetting("password", "");
        }
        
        _connectionString = builder.ConnectionString;
    }
    
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            
            await using var cmd = new SqlCommand("SELECT 1", conn);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            
            _logger?.LogDebug("SQL Server connection test: {Server}/{Database}", 
                _config.GetSetting("server", ""), 
                _config.GetSetting("database", ""));
            
            return result != null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SQL Server availability check failed");
            return false;
        }
    }
    
    public async Task<string> BackupAsync(string outputPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var database = _config.GetSetting("database", "");
        var backupType = _config.GetSetting("backupType", "full").ToLower();
        var checkIntegrity = _config.GetSetting("checkIntegrity", "false").ToLower() == "true";
        
        _logger?.LogInformation("Starting {BackupType} backup of database {Database} to {Path}", 
            backupType, database, outputPath);
        
        progress?.Report(0);
        
        // Проверка целостности базы
        if (checkIntegrity)
        {
            _logger?.LogInformation("Running DBCC CHECKDB on {Database}", database);
            await RunDbccCheckAsync(cancellationToken);
            progress?.Report(5);
        }
        
        // Создание резервной копии
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        
        var backupCommandText = $@"
            BACKUP DATABASE [{database.Replace("]", "]]")}] 
            TO DISK = @Path
            WITH {(backupType == "differential" ? "DIFFERENTIAL" : "INIT, COMPRESSION, STATS = 10")}";
        
        await using var cmd = new SqlCommand(backupCommandText, conn);
        cmd.Parameters.AddWithValue("@Path", outputPath);
        
        // Обработка прогресса через информационные сообщения
        conn.InfoMessage += (sender, args) =>
        {
            foreach (SqlError error in args.Errors)
            {
                if (int.TryParse(error.Message, out int percent))
                {
                    progress?.Report(5 + (percent * 0.9)); // 5-95%
                }
            }
        };
        
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        
        progress?.Report(100);
        
        _logger?.LogInformation("Backup completed: {Path}", outputPath);
        
        return outputPath;
    }
    
    public async Task<long> GetEstimatedSizeAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        
        var sql = @"
            SELECT SUM(size) * 8 * 1024 
            FROM sys.database_files 
            WHERE type IN (0, 2)";
        
        await using var cmd = new SqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        
        return result != null ? Convert.ToInt64(result) : 0;
    }
    
    private async Task RunDbccCheckAsync(CancellationToken cancellationToken)
    {
        var database = _config.GetSetting("database", "");
        
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        
        var sql = $@"
            DBCC CHECKDB ([{database.Replace("]", "]]")}] ) 
            WITH NO_INFOMSGS, ALL_ERRORMSGS";
        
        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = 0; // Без таймаута
        
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
    
    /// <summary>
    /// Создание имени файла для бекапа
    /// </summary>
    public static string GetBackupFileName(string database, string backupType = "full")
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var typeSuffix = backupType switch
        {
            "differential" => "_DIFF",
            "transaction" => "_LOG",
            _ => "_FULL"
        };
        
        return $"{database}{typeSuffix}_{timestamp}.bak";
    }
}

/// <summary>
/// Фабрика для создания источников SQL Server
/// </summary>
public class SqlServerSourceFactory
{
    private readonly ILogger<SqlServerSource>? _logger;
    
    public SqlServerSourceFactory(ILogger<SqlServerSource>? logger = null)
    {
        _logger = logger;
    }
    
    public IBackupSource Create(SourceConfig config)
    {
        return new SqlServerSource(config, _logger);
    }
}
