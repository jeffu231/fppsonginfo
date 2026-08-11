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
        var writer = new RecordingSongInfoWriter();
        var service = CreateService(mqttClient, writer);

        await service.StartAsync(CancellationToken.None);
        await mqttClient.WaitForSubscriptionAsync();
        await mqttClient.PublishMessageAsync(MqttMessageFactory.Create("root/radio/songinfo", payload));

        await Task.Delay(50);
        Assert.Empty(writer.Updates);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WritesValidExactTopicPayloadAsync()
    {
        var mqttClient = new TestMqttClient();
        var writer = new RecordingSongInfoWriter();
        var service = CreateService(mqttClient, writer);

        await service.StartAsync(CancellationToken.None);
        await mqttClient.WaitForSubscriptionAsync();
        await mqttClient.PublishMessageAsync(MqttMessageFactory.Create("root/radio/songinfo", "{\"artist\":\"Artist\",\"title\":\"Title\"}"));

        await AsyncAssert.EventuallyAsync(() => writer.Updates.Count == 1);
        Assert.Equal(new SongInfo("Artist", "Title"), writer.Updates[0]);
        await service.StopAsync(CancellationToken.None);
    }

    private static RadioAutomationConsumerService CreateService(TestMqttClient mqttClient, RecordingSongInfoWriter writer) =>
        new(
            mqttClient,
            Options.Create(new MqttOptions { Enabled = true, RootTopic = "root" }),
            Options.Create(new RadioAutomationOptions { SongTopic = "/radio/songinfo" }),
            NullLogger<RadioAutomationConsumerService>.Instance,
            writer);
}
