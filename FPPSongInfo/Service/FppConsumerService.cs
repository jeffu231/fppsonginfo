using System.Text;
using MQTTnet.Client;
using IMqttClient = FPPSongInfo.Mqtt.IMqttClient;

namespace FPPSongInfo.Service;

public class FppConsumerService:BackgroundService
{
    private readonly IMqttClient _mqttClient;
    private readonly ILogger<FppConsumerService> _logger;
    private readonly IConfiguration _config;
    private string _artist = string.Empty;
    private string _title = string.Empty;
    
    public FppConsumerService(IMqttClient mqttClient, IConfiguration configuration, ILogger<FppConsumerService> logger)
    {
        _mqttClient = mqttClient;
        _config = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("FPP Consumer Service Execute");
       
        while (!_mqttClient.IsConnected)
        {
            await Task.Delay(1000, stoppingToken);
            _logger.LogDebug("Waiting for MQTT to Connect");
        }
        
        _mqttClient.OnMessageReceived += MqttClientOnOnMessageReceived;
        var songTopic = _config.GetValue<string>("Mqtt:RootTopic") + _config.GetValue<string>("Mqtt:SongTopic") + "/#";
        _logger.LogDebug("Subscribing to Topic {Topic}", songTopic);
        
        await _mqttClient.SubscribeAsync(songTopic);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
        await _mqttClient.SubscribeAsync(songTopic);
        _logger.LogDebug("FPP Consumer Service execute finishing");
    }
    
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FPP Consumer Service is stopping.");

        await base.StopAsync(stoppingToken);
    }

    private async void MqttClientOnOnMessageReceived(object? sender, MqttApplicationMessageReceivedEventArgs e)
    {
        _logger.LogDebug("Message received on Topic {Topic}", e.ApplicationMessage.Topic);
        if (e.ApplicationMessage.Topic.EndsWith("artist"))
        {
            _artist = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            await UpdateSongInfo(_artist, _title);
            _logger.LogDebug("Received artist message: {ApplicationMessageTopic}, {PayloadSegment}", e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
        }
        else if(e.ApplicationMessage.Topic.EndsWith("title"))
        {
            _title = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            _logger.LogDebug("Received title message: {ApplicationMessageTopic}, {PayloadSegment}", e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
        }
    }

    private async Task UpdateSongInfo(string artist, string title)
    {
        FileStream? fs = null;
        try
        {
            var filePath = _config.GetValue<string>("Output:FilePath");
            var fileName = _config.GetValue<string>("Output:FileName");
            if (!string.IsNullOrEmpty(filePath) && !string.IsNullOrEmpty(fileName))
            {
                if (!Directory.Exists(filePath))
                {
                    Directory.CreateDirectory(filePath);
                }
                var path = Path.Combine(filePath,fileName);
                fs = File.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                await using TextWriter tw = new StreamWriter(fs);
                await tw.WriteLineAsync($"{artist}{(string.IsNullOrEmpty(artist) ? string.Empty : " - ")}{title}");
            }
                
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to write song file");
        }
        finally
        {
            if (fs != null)
            {
                fs.Dispose();
            }
        }
    }
}