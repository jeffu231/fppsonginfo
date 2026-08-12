namespace FPPSongInfo.Service;

internal interface ISongInfoSink
{
    Task UpdateAsync(SongInfo songInfo, CancellationToken cancellationToken);
}
