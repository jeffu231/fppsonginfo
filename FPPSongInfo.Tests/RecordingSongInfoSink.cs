namespace FPPSongInfo.Tests;

internal sealed class RecordingSongInfoSink : ISongInfoSink
{
    private readonly List<SongInfo> _updates = [];

    public IReadOnlyList<SongInfo> Updates
    {
        get
        {
            lock (_updates)
            {
                return _updates.ToArray();
            }
        }
    }

    public Task UpdateAsync(SongInfo songInfo, CancellationToken cancellationToken)
    {
        lock (_updates)
        {
            _updates.Add(songInfo);
        }

        return Task.CompletedTask;
    }
}
