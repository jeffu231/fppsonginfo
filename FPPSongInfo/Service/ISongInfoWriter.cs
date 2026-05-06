namespace FPPSongInfo.Service;

public interface ISongInfoWriter
{
    Task UpdateSongInfo(string artist, string title);
}
