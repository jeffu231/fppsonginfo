using FPPSongInfo.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPPSongInfo.Tests;

public sealed class SongInfoWriterTests
{
    [Fact]
    public async Task SerializesConcurrentWritesAndCleansTemporaryFilesAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            using var writer = CreateWriter(directory);
            var songs = Enumerable.Range(0, 20).Select(index => new SongInfo($"Artist {index}", $"Title {index}")).ToArray();
            await Task.WhenAll(songs.Select(song => writer.UpdateSongInfoAsync(song, CancellationToken.None)));

            var output = await File.ReadAllTextAsync(Path.Combine(directory, "CurrentSong.txt"));
            Assert.Contains(songs, song => output == $"{song.Artist} - {song.Title}{Environment.NewLine}");
            Assert.Empty(Directory.GetFiles(directory, ".CurrentSong.txt.*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task PropagatesDirectoryWriteFailuresAsync()
    {
        var path = Path.GetTempFileName();

        try
        {
            using var writer = CreateWriter(path);
            await Assert.ThrowsAnyAsync<IOException>(() => writer.UpdateSongInfoAsync(new SongInfo("Artist", "Title"), CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SongInfoWriter CreateWriter(string directory) =>
        new(
            Options.Create(new OutputOptions { FilePath = directory, FileName = "CurrentSong.txt" }),
            NullLogger<SongInfoWriter>.Instance);
}
