namespace FPPSongInfo.Tests;

internal sealed class ThrowingSongInfoSink : ISongInfoSink
{
    public Task UpdateAsync(SongInfo songInfo, CancellationToken cancellationToken) =>
        Task.FromException(new IOException("Simulated sink failure."));
}
