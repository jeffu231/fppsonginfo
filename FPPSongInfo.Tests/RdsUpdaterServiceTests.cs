using FPPSongInfo.Configuration;
using FPPSongInfo.Service;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace FPPSongInfo.Tests;

public sealed class RdsUpdaterServiceTests
{
    [Fact]
    public async Task RetainsOnlyTheNewestSongBeforeTheWorkerStartsAsync()
    {
        var deviceClient = new RecordingMrds192DeviceClient();
        var service = CreateService(deviceClient, new FakeTimeProvider(), TimeSpan.FromHours(1));

        await service.UpdateAsync(new SongInfo("First", "Song"), CancellationToken.None);
        await service.UpdateAsync(new SongInfo("Latest", "Song"), CancellationToken.None);

        Assert.Equal(0, deviceClient.InitializationCount);
        Assert.Empty(deviceClient.RadioTexts);

        await service.StartAsync(CancellationToken.None);
        await AsyncAssert.EventuallyAsync(() => deviceClient.RadioTexts.Count == 1);

        Assert.Equal("Latest - Song", deviceClient.RadioTexts[0]);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RotatesConfiguredRadioTextAfterTheCurrentSongAsync()
    {
        var deviceClient = new RecordingMrds192DeviceClient();
        var timeProvider = new FakeTimeProvider();
        var service = CreateService(deviceClient, timeProvider, TimeSpan.FromSeconds(30));

        await service.StartAsync(CancellationToken.None);
        await service.UpdateAsync(new SongInfo("Artist", "Title"), CancellationToken.None);

        await AsyncAssert.EventuallyAsync(() => deviceClient.RadioTexts.Count == 1);
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        await AsyncAssert.EventuallyAsync(() => deviceClient.RadioTexts.Count >= 2);

        Assert.Equal("Artist - Title", deviceClient.RadioTexts[0]);
        Assert.Equal("lightshow.onthecompound.org", deviceClient.RadioTexts[1]);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SkipsEmptySlotsAndResetsToTheLatestSongAsync()
    {
        var deviceClient = new RecordingMrds192DeviceClient();
        var timeProvider = new FakeTimeProvider();
        var service = CreateService(
            deviceClient,
            timeProvider,
            TimeSpan.FromSeconds(30),
            ["", "  ", "lightshow.onthecompound.org"]);

        await service.StartAsync(CancellationToken.None);
        await service.UpdateAsync(new SongInfo("First", "Song"), CancellationToken.None);
        await AsyncAssert.EventuallyAsync(() => deviceClient.RadioTexts.Count == 1);

        timeProvider.Advance(TimeSpan.FromSeconds(30));
        await AsyncAssert.EventuallyAsync(() => deviceClient.RadioTexts.Count == 2);
        await service.UpdateAsync(new SongInfo("Latest", "Song"), CancellationToken.None);
        await AsyncAssert.EventuallyAsync(() => deviceClient.RadioTexts.Count == 3);

        Assert.Equal(
            ["First - Song", "lightshow.onthecompound.org", "Latest - Song"],
            deviceClient.RadioTexts);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RetriesInitializationAndReplaysTheLatestSongAsync()
    {
        var deviceClient = new RecordingMrds192DeviceClient { RemainingInitializationFailures = 2 };
        var timeProvider = new FakeTimeProvider();
        var service = CreateService(deviceClient, timeProvider, TimeSpan.FromHours(1));

        await service.UpdateAsync(new SongInfo("First", "Song"), CancellationToken.None);
        await service.UpdateAsync(new SongInfo("Latest", "Song"), CancellationToken.None);
        await service.StartAsync(CancellationToken.None);
        await AsyncAssert.EventuallyAsync(() => deviceClient.InitializationCount == 1);
        await Task.Delay(20);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await AsyncAssert.EventuallyAsync(() => deviceClient.InitializationCount == 2);
        await Task.Delay(20);
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        await AsyncAssert.EventuallyAsync(() => deviceClient.InitializationCount == 3);
        await AsyncAssert.EventuallyAsync(() => deviceClient.RadioTexts.Count == 1);

        Assert.Equal("Latest - Song", deviceClient.RadioTexts[0]);
        await service.StopAsync(CancellationToken.None);
    }

    private static RdsUpdaterService CreateService(
        RecordingMrds192DeviceClient deviceClient,
        TimeProvider timeProvider,
        TimeSpan radioTextRotationInterval,
        List<string>? additionalRadioTextMessages = null) =>
        new(
            new RecordingMrds192Bus([]),
            deviceClient,
            Options.Create(
                new RdsOptions
                {
                    Enabled = true,
                    PortName = "COM3",
                    RadioTextRotationInterval = radioTextRotationInterval,
                    AdditionalRadioTextMessages = additionalRadioTextMessages ?? ["lightshow.onthecompound.org"]
                }),
            timeProvider,
            NullLogger<RdsUpdaterService>.Instance);
}
