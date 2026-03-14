namespace BackupSystem.Core;

/// <summary>
/// Настройки архивирования
/// </summary>
public class ArchiverSettings
{
    public bool Enabled { get; set; } = true;
    public string Format { get; set; } = "zip";
    public string CompressionLevel { get; set; } = "normal";
    public long SplitSize { get; set; } = 0;
    public string? Password { get; set; }
    public bool DeleteSourceAfterArchive { get; set; } = false;
}

public class RetentionSettings
{
    public int KeepDaily { get; set; } = 7;
    public int KeepWeekly { get; set; } = 4;
    public int KeepMonthly { get; set; } = 12;
    public long MinFreeSpace { get; set; } = 1024 * 1024 * 1024;
}

public class NotificationSettings
{
    public string? Email { get; set; }
    public bool OnSuccess { get; set; } = false;
    public bool OnFailure { get; set; } = true;
    public SmtpSettings? Smtp { get; set; }
    public TelegramSettings? Telegram { get; set; }
}

public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
}

public class TelegramSettings
{
    public bool Enabled { get; set; } = false;
    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
}

public class BackupHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string JobId { get; set; } = "";
    public string JobName { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? LocalFilePath { get; set; }
    public List<string> RemoteFilePaths { get; set; } = new();
    public long SizeBytes { get; set; }
    public string? Hash { get; set; }
}
