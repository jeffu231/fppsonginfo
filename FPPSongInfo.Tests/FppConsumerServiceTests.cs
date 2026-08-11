using FPPSongInfo.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPPSongInfo.Tests;

public sealed class FppConsumerServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WritesLatestArtistAndTitleRegardlessOfArrivalOrderAsync(bool artistFirst)
    {
        var mqttClient = new TestMqttClient();
        var writer = new RecordingSongInfoWriter();
        var service = CreateService(mqttClient, writer);

        await service.StartAsync(CancellationToken.None);
        await mqttClient.WaitForSubscriptionAsync();

        var artist = MqttMessageFactory.Create("root/fpp/artist", "Artist");
        var title = MqttMessageFactory.Create("root/fpp/title", "Title");
        await mqttClient.PublishMessageAsync(artistFirst ? artist : title);
        await mqttClient.PublishMessageAsync(artistFirst ? title : artist);

        await AsyncAssert.EventuallyAsync(() => writer.Updates.Count == 2);
        Assert.Equal(new SongInfo("Artist", "Title"), writer.Updates[^1]);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task IgnoresLookalikeTopicsAndDetachesOnStopAsync()
    {
        var mqttClient = new TestMqttClient();
        var writer = new RecordingSongInfoWriter();
        var service = CreateService(mqttClient, writer);

        await service.StartAsync(CancellationToken.None);
        await mqttClient.WaitForSubscriptionAsync();
        await mqttClient.PublishMessageAsync(MqttMessageFactory.Create("root/fpp/notartist", "Ignored"));

        await Task.Delay(50);
        Assert.Empty(writer.Updates);

        await service.StopAsync(CancellationToken.None);
        Assert.Equal(0, mqttClient.HandlerCount);
        Assert.Contains("root/fpp/#", mqttClient.Unsubscriptions);
    }

    private static FppConsumerService CreateService(TestMqttClient mqttClient, RecordingSongInfoWriter writer) =>
        new(
            mqttClient,
            Options.Create(new MqttOptions { Enabled = true, RootTopic = "root" }),
            Options.Create(new FppOptions { SongTopic = "/fpp" }),
            NullLogger<FppConsumerService>.Instance,
            writer);
}
