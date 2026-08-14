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

    [Fact]
    public async Task SerializesConcurrentPublicationsAsync()
    {
        var blockingSink = new BlockingSongInfoSink();
        using var publisher = new SongInfoPublisher([blockingSink], NullLogger<SongInfoPublisher>.Instance);

        var firstPublication = publisher.PublishAsync(new SongInfo("First", "Song"), CancellationToken.None);
        await AsyncAssert.EventuallyAsync(() => blockingSink.MaximumConcurrentUpdates == 1);
        var secondPublication = publisher.PublishAsync(new SongInfo("Second", "Song"), CancellationToken.None);

        await Task.Delay(20);
        Assert.Equal(1, blockingSink.MaximumConcurrentUpdates);

        blockingSink.Release();
        await Task.WhenAll(firstPublication, secondPublication);

        Assert.Equal(1, blockingSink.MaximumConcurrentUpdates);
    }
}
