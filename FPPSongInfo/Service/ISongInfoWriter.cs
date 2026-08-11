namespace FPPSongInfo.Service;

internal interface ISongInfoWriter
{
    Task UpdateSongInfoAsync(SongInfo songInfo, CancellationToken cancellationToken);
}
