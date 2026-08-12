namespace FPPSongInfo.Tests;

internal sealed class BlockingSongInfoSink : ISongInfoSink
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _activeUpdates;

    public int MaximumConcurrentUpdates { get; private set; }

    public Task UpdateAsync(SongInfo songInfo, CancellationToken cancellationToken)
    {
        var activeUpdates = Interlocked.Increment(ref _activeUpdates);
        MaximumConcurrentUpdates = Math.Max(MaximumConcurrentUpdates, activeUpdates);
        return WaitForReleaseAsync();
    }

    public void Release() => _release.TrySetResult();

    private async Task WaitForReleaseAsync()
    {
        try
        {
            await _release.Task;
        }
        finally
        {
            Interlocked.Decrement(ref _activeUpdates);
        }
    }
}
