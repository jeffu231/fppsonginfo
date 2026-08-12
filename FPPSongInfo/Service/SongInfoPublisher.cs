namespace FPPSongInfo.Service;

internal sealed class SongInfoPublisher(
    IEnumerable<ISongInfoSink> songInfoSinks,
    ILogger<SongInfoPublisher> logger) : ISongInfoPublisher, IDisposable
{
    private readonly ILogger<SongInfoPublisher> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IReadOnlyList<ISongInfoSink> _songInfoSinks = songInfoSinks?.ToArray()
        ?? throw new ArgumentNullException(nameof(songInfoSinks));
    private readonly SemaphoreSlim _publishLock = new(1, 1);

    public async Task PublishAsync(SongInfo songInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(songInfo);

        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var songInfoSink in _songInfoSinks)
            {
                try
                {
                    await songInfoSink.UpdateAsync(songInfo, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Song information sink {SinkType} failed to process published song information",
                        songInfoSink.GetType().Name);
                }
            }
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public void Dispose() => _publishLock.Dispose();
}
