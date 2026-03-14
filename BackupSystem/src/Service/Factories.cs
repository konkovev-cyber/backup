using BackupSystem.Core;

namespace BackupSystem.Service;

public class BackupSourceFactory : IBackupSourceFactory
{
    private readonly IEnumerable<IBackupSource> _sources;

    public BackupSourceFactory(IEnumerable<IBackupSource> sources)
    {
        _sources = sources;
    }

    public IBackupSource? Create(string sourceId)
    {
        return _sources.FirstOrDefault(s => s.Id == sourceId);
    }
}

public class BackupDestinationFactory : IBackupDestinationFactory
{
    private readonly IEnumerable<IBackupDestination> _destinations;

    public BackupDestinationFactory(IEnumerable<IBackupDestination> destinations)
    {
        _destinations = destinations;
    }

    public IBackupDestination? Create(string destinationId)
    {
        return _destinations.FirstOrDefault(d => d.Id == destinationId);
    }
}
