using FPPSongInfo.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPPSongInfo.Tests;

public sealed class RadioAutomationConsumerServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    public async Task IgnoresEmptyAndMalformedPayloadsAsync(string payload)
    {
        var mqttClient = new TestMqttClient();
        var publisher = new RecordingSongInfoPublisher();
        var service = CreateService(mqttClient, publisher);

        await service.StartAsync(CancellationToken.None);
        await mqttClient.WaitForSubscriptionAsync();
        await mqttClient.PublishMessageAsync(MqttMessageFactory.Create("root/radio/songinfo", payload));

        await Task.Delay(50);
        Assert.Empty(publisher.Publications);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PublishesValidExactTopicPayloadAsync()
    {
        var mqttClient = new TestMqttClient();
        var publisher = new RecordingSongInfoPublisher();
        var service = CreateService(mqttClient, publisher);

        await service.StartAsync(CancellationToken.None);
        await mqttClient.WaitForSubscriptionAsync();
        await mqttClient.PublishMessageAsync(MqttMessageFactory.Create("root/radio/songinfo", "{\"artist\":\"Artist\",\"title\":\"Title\"}"));

        await AsyncAssert.EventuallyAsync(() => publisher.Publications.Count == 1);
        Assert.Equal(new SongInfo("Artist", "Title"), publisher.Publications[0]);
        await service.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("{\"artist\":\"\",\"title\":\"Title\"}")]
    [InlineData("{\"artist\":\"Artist\",\"title\":\" \"}")]
    public async Task IgnoresIncompletePayloadsAsync(string payload)
    {
        var mqttClient = new TestMqttClient();
        var publisher = new RecordingSongInfoPublisher();
        var service = CreateService(mqttClient, publisher);

        await service.StartAsync(CancellationToken.None);
        await mqttClient.WaitForSubscriptionAsync();
        await mqttClient.PublishMessageAsync(MqttMessageFactory.Create("root/radio/songinfo", payload));

        await Task.Delay(50);
        Assert.Empty(publisher.Publications);
        await service.StopAsync(CancellationToken.None);
    }

    private static RadioAutomationConsumerService CreateService(TestMqttClient mqttClient, RecordingSongInfoPublisher publisher) =>
        new(
            mqttClient,
            Options.Create(new MqttOptions { Enabled = true, RootTopic = "root" }),
            Options.Create(new RadioAutomationOptions { SongTopic = "/radio/songinfo" }),
            NullLogger<RadioAutomationConsumerService>.Instance,
            publisher);
}
