using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;

namespace FPPSongInfo.Tests;

internal static class MqttMessageFactory
{
    public static MqttApplicationMessageReceivedEventArgs Create(string topic, string payload)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .Build();

        return new MqttApplicationMessageReceivedEventArgs(
            "test-client",
            message,
            new MqttPublishPacket(),
            static (_, _) => Task.CompletedTask);
    }
}
