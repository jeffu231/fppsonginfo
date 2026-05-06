namespace FPPSongInfo.Service;

public class SongInfoWriter(IConfiguration configuration, ILogger<SongInfoWriter> logger) : ISongInfoWriter
{
    public async Task UpdateSongInfo(string artist, string title)
    {
        FileStream? fs = null;
        try
        {
            var filePath = configuration.GetValue<string>("Output:FilePath");
            var fileName = configuration.GetValue<string>("Output:FileName");
            if (!string.IsNullOrEmpty(filePath) && !string.IsNullOrEmpty(fileName))
            {
                if (!Directory.Exists(filePath))
                {
                    Directory.CreateDirectory(filePath);
                }

                var path = Path.Combine(filePath, fileName);
                logger.LogDebug("Writing song info to path {Path} for {Artist} - {Title}", path, artist, title);
                fs = File.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                await using TextWriter tw = new StreamWriter(fs);
                await tw.WriteLineAsync($"{artist}{(string.IsNullOrEmpty(artist) ? string.Empty : " - ")}{title}");
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unable to write song file");
        }
        finally
        {
            if (fs != null)
            {
                await fs.DisposeAsync();
            }
        }
    }
}
