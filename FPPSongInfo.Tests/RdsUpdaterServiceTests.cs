using FPPSongInfo.Configuration;
using FPPSongInfo.Service;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPPSongInfo.Tests;

public sealed class RdsUpdaterServiceTests
{
    [Fact]
    public async Task RetainsOnlyTheNewestSongBeforeTheWorkerStartsAsync()
    {
        var deviceClient = new RecordingMrds192DeviceClient();
        var service = CreateService(deviceClient, TimeSpan.FromHours(1));

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
        var service = CreateService(deviceClient, TimeSpan.FromMilliseconds(10));

        await service.StartAsync(CancellationToken.None);
        await service.UpdateAsync(new SongInfo("Artist", "Title"), CancellationToken.None);

        await AsyncAssert.EventuallyAsync(() => deviceClient.RadioTexts.Count >= 2);

        Assert.Equal("Artist - Title", deviceClient.RadioTexts[0]);
        Assert.Equal("lightshow.onthecompound.org", deviceClient.RadioTexts[1]);
        await service.StopAsync(CancellationToken.None);
    }

    private static RdsUpdaterService CreateService(
        RecordingMrds192DeviceClient deviceClient,
        TimeSpan radioTextRotationInterval) =>
        new(
            new RecordingMrds192Bus([]),
            deviceClient,
            Options.Create(
                new RdsOptions
                {
                    Enabled = true,
                    PortName = "COM3",
                    RadioTextRotationInterval = radioTextRotationInterval,
                    AdditionalRadioTextMessages = ["lightshow.onthecompound.org"]
                }),
            TimeProvider.System,
            NullLogger<RdsUpdaterService>.Instance);
}
