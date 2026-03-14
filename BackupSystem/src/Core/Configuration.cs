namespace BackupSystem.Core;

/// <summary>
/// Конфигурация системы резервного копирования
/// </summary>
public class BackupConfiguration
{
    public string Version { get; set; } = "1.0";
    public List<SourceConfig> Sources { get; set; } = new();
    public List<DestinationConfig> Destinations { get; set; } = new();
    public List<JobConfig> Jobs { get; set; } = new();
    public List<BackupHistory> History { get; set; } = new();
    public GlobalSettings Global { get; set; } = new();
}

public class SourceConfig
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object> Settings { get; set; } = new();

    public string GetSetting(string key, string defaultValue = "")
    {
        if (Settings.TryGetValue(key, out var value))
        {
            if (value is System.Text.Json.JsonElement element)
            {
                if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
                    return string.Join(";", element.EnumerateArray().Select(e => e.ToString()));
                return element.ToString();
            }
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }
}

public class DestinationConfig
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object> Settings { get; set; } = new();

    public string GetSetting(string key, string defaultValue = "")
    {
        if (Settings.TryGetValue(key, out var value))
        {
            if (value is System.Text.Json.JsonElement element)
            {
                return element.ToString();
            }
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }
}

public class JobConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Ожидание";
    public bool Enabled { get; set; } = true;
    public List<string> SourceIds { get; set; } = new();
    public List<string> DestinationIds { get; set; } = new();
    public ScheduleConfig? Schedule { get; set; } = new();
    public ArchiverSettings? Archiver { get; set; } = new();
    public RetentionSettings? Retention { get; set; } = new();
    public NotificationSettings? Notifications { get; set; } = new();
    public HookSettings? Hooks { get; set; } = new();
}

public class HookSettings
{
    public string? BeforeScript { get; set; }
    public string? AfterScript { get; set; }
}

public class ScheduleConfig
{
    public string Type { get; set; } = "daily";
    public string? Time { get; set; }
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public int? IntervalMinutes { get; set; }
}

public class GlobalSettings
{
    public string TempPath { get; set; } = Path.Combine(Path.GetTempPath(), "BackupSystem");
    public string LogPath { get; set; } = "logs";
    public string LogLevel { get; set; } = "Info";
    public int MaxConcurrentJobs { get; set; } = 1;
    public int OperationTimeoutMinutes { get; set; } = 60;
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 30;
    public SmtpSettings Smtp { get; set; } = new();
    public TelegramSettings Telegram { get; set; } = new();
}
