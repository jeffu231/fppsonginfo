namespace FPPSongInfo.Service;

internal interface ISongInfoPublisher
{
    Task PublishAsync(SongInfo songInfo, CancellationToken cancellationToken);
}
