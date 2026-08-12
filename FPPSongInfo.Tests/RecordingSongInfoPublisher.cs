namespace FPPSongInfo.Tests;

internal sealed class RecordingSongInfoPublisher : ISongInfoPublisher
{
    private readonly List<SongInfo> _publications = [];

    public IReadOnlyList<SongInfo> Publications
    {
        get
        {
            lock (_publications)
            {
                return _publications.ToArray();
            }
        }
    }

    public Task PublishAsync(SongInfo songInfo, CancellationToken cancellationToken)
    {
        lock (_publications)
        {
            _publications.Add(songInfo);
        }

        return Task.CompletedTask;
    }
}
