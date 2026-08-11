using System.Text;
using FPPSongInfo.Configuration;
using Microsoft.Extensions.Options;
using MQTTnet.Client;
using IMqttClient = FPPSongInfo.Mqtt.IMqttClient;

namespace FPPSongInfo.Service;

internal sealed class FppConsumerService:BackgroundService
{
    private readonly IMqttClient _mqttClient;
    private readonly ILogger<FppConsumerService> _logger;
    private readonly MqttOptions _mqttOptions;
    private readonly FppOptions _fppOptions;
    private readonly ISongInfoWriter _songInfoWriter;
    private string _artist = string.Empty;
    private string _title = string.Empty;
    
    public FppConsumerService(
        IMqttClient mqttClient,
        IOptions<MqttOptions> mqttOptions,
        IOptions<FppOptions> fppOptions,
        ILogger<FppConsumerService> logger,
        ISongInfoWriter songInfoWriter)
    {
        _mqttClient = mqttClient;
        _mqttOptions = mqttOptions.Value;
        _fppOptions = fppOptions.Value;
        _logger = logger;
        _songInfoWriter = songInfoWriter;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("FPP Consumer Service Execute");
        _mqttClient.OnMessageReceived += MqttClientOnOnMessageReceived;
        var songTopic = _mqttOptions.RootTopic + _fppOptions.SongTopic + "/#";
        _logger.LogDebug("Subscribing to Topic {Topic}", songTopic);
        while (!_mqttClient.IsConnected)
        {
            await Task.Delay(1000, stoppingToken);
            _logger.LogDebug("Waiting for MQTT to Connect");
        }
        _logger.LogDebug("MQTT is Connected");
        await _mqttClient.SubscribeAsync(songTopic);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
        await _mqttClient.UnsubscribeAsync(songTopic);
        _logger.LogDebug("FPP Consumer Service execute finishing");
    }
    
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FPP Consumer Service is stopping");

        await base.StopAsync(stoppingToken);
    }

    private async void MqttClientOnOnMessageReceived(object? sender, MqttApplicationMessageReceivedEventArgs e)
    {
        _logger.LogDebug("Message received on Topic {Topic}", e.ApplicationMessage.Topic);
        if (e.ApplicationMessage.Topic.EndsWith("artist"))
        {
            _artist = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            await _songInfoWriter.UpdateSongInfo(_artist, _title);
            _logger.LogDebug("Received artist message: {ApplicationMessageTopic}, {PayloadSegment}", e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
        }
        else if(e.ApplicationMessage.Topic.EndsWith("title"))
        {
            _title = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            _logger.LogDebug("Received title message: {ApplicationMessageTopic}, {PayloadSegment}", e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
        }
    }
}
