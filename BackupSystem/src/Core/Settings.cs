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
