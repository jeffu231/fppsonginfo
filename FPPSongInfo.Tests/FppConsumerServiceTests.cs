using FPPSongInfo.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPPSongInfo.Tests;

public sealed class FppConsumerServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublishesCompleteArtistAndTitleRegardlessOfArrivalOrderAsync(bool artistFirst)
    {
        var mqttClient = new TestMqttClient();
        var publisher = new RecordingSongInfoPublisher();
        var service = CreateService(mqttClient, publisher);

        await service.StartAsync(CancellationToken.None);
        await mqttClient.WaitForSubscriptionAsync();

        var artist = MqttMessageFactory.Create("root/fpp/artist", "Artist");
        var title = MqttMessageFactory.Create("root/fpp/title", "Title");
        await mqttClient.PublishMessageAsync(artistFirst ? artist : title);
        await mqttClient.PublishMessageAsync(artistFirst ? title : artist);

        await AsyncAssert.EventuallyAsync(() => publisher.Publications.Count == 1);
        Assert.Equal(new SongInfo("Artist", "Title"), publisher.Publications[0]);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task IgnoresLookalikeTopicsAndDetachesOnStopAsync()
    {
        var mqttClient = new TestMqttClient();
        var publisher = new RecordingSongInfoPublisher();
        var service = CreateService(mqttClient, publisher);

        await service.StartAsync(CancellationToken.None);
        await mqttClient.WaitForSubscriptionAsync();
        await mqttClient.PublishMessageAsync(MqttMessageFactory.Create("root/fpp/notartist", "Ignored"));

        await Task.Delay(50);
        Assert.Empty(publisher.Publications);

        await service.StopAsync(CancellationToken.None);
        Assert.Equal(0, mqttClient.HandlerCount);
        Assert.Contains("root/fpp/#", mqttClient.Unsubscriptions);
    }

    [Fact]
    public async Task WaitsForNonblankArtistAndTitleBeforePublishingAsync()
    {
        var mqttClient = new TestMqttClient();
        var publisher = new RecordingSongInfoPublisher();
        var service = CreateService(mqttClient, publisher);

        await service.StartAsync(CancellationToken.None);
        await mqttClient.WaitForSubscriptionAsync();
        await mqttClient.PublishMessageAsync(MqttMessageFactory.Create("root/fpp/artist", "Artist"));
        await mqttClient.PublishMessageAsync(MqttMessageFactory.Create("root/fpp/title", " "));

        await Task.Delay(50);
        Assert.Empty(publisher.Publications);

        await mqttClient.PublishMessageAsync(MqttMessageFactory.Create("root/fpp/title", " Title "));
        await AsyncAssert.EventuallyAsync(() => publisher.Publications.Count == 1);
        Assert.Equal(new SongInfo("Artist", "Title"), publisher.Publications[0]);
        await service.StopAsync(CancellationToken.None);
    }

    private static FppConsumerService CreateService(TestMqttClient mqttClient, RecordingSongInfoPublisher publisher) =>
        new(
            mqttClient,
            Options.Create(new MqttOptions { Enabled = true, RootTopic = "root" }),
            Options.Create(new FppOptions { SongTopic = "/fpp" }),
            NullLogger<FppConsumerService>.Instance,
            publisher);
}
