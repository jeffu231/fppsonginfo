using System.Text;
using MQTTnet.Client;
using IMqttClient = FPPSongInfo.Mqtt.IMqttClient;

namespace FPPSongInfo.Service;

public class RadioAutomationConsumerService(
    IMqttClient mqttClient,
    IConfiguration configuration,
    ILogger<RadioAutomationConsumerService> logger,
    ISongInfoWriter songInfoWriter)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("Radio Automation Consumer Service Execute");
        mqttClient.OnMessageReceived += MqttClientOnOnMessageReceived;
        var songTopic = configuration.GetValue<string>("Mqtt:RootTopic") + configuration.GetValue<string>("RadioAutomation:SongTopic") + "/#";
        logger.LogDebug("Subscribing to Topic {Topic}", songTopic);
        while (!mqttClient.IsConnected)
        {
            await Task.Delay(1000, stoppingToken);
            logger.LogDebug("Waiting for MQTT to Connect");
        }
        logger.LogDebug("MQTT is Connected");
        await mqttClient.SubscribeAsync(songTopic);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
        await mqttClient.UnsubscribeAsync(songTopic);
        logger.LogDebug("FPP Consumer Service execute finishing");
    }
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Radio Automation Consumer Service is stopping");

        await base.StopAsync(stoppingToken);
    }

    private async void MqttClientOnOnMessageReceived(object? sender, MqttApplicationMessageReceivedEventArgs e)
    {
        logger.LogDebug("Message received on Topic {Topic}", e.ApplicationMessage.Topic);
        if (e.ApplicationMessage.Topic.EndsWith("artist"))
        {
            var songInfoPayload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            var songInfo = System.Text.Json.JsonSerializer.Deserialize<SongInfo>(songInfoPayload);
            logger.LogDebug("Received song info message: {ApplicationMessageTopic}, {PayloadSegment}", e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
            
            if (songInfo != null)
            {
                await songInfoWriter.UpdateSongInfo(songInfo.Artist, songInfo.Title);
            }
        }
    }

    private class SongInfo
    {
        public string Artist { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Album { get; init; } =  string.Empty;
    }
}
