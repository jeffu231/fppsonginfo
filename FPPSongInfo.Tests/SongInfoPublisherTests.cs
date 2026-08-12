using Microsoft.Extensions.Logging.Abstractions;

namespace FPPSongInfo.Tests;

public sealed class SongInfoPublisherTests
{
    [Fact]
    public async Task DeliversToRemainingSinksWhenASinkFailsAsync()
    {
        var recordingSink = new RecordingSongInfoSink();
        using var publisher = new SongInfoPublisher(
            [new ThrowingSongInfoSink(), recordingSink],
            NullLogger<SongInfoPublisher>.Instance);
        var song = new SongInfo("Artist", "Title");

        await publisher.PublishAsync(song, CancellationToken.None);

        Assert.Equal([song], recordingSink.Updates);
    }
}
