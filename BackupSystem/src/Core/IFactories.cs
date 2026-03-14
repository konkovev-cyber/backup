namespace BackupSystem.Core;

public interface IBackupSourceFactory
{
    IBackupSource? Create(string sourceId);
}

public interface IBackupDestinationFactory
{
    IBackupDestination? Create(string destinationId);
}
