using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;

namespace FPPSongInfo.Mqtt;

public class MqttClient:IMqttClient
{
    private readonly IManagedMqttClient _mqttClient;
    private readonly ILogger<MqttClient> _logger;

    public event EventHandler<MqttApplicationMessageReceivedEventArgs>? OnMessageReceived;

    public MqttClient(IConfiguration configuration, ILogger<MqttClient> logger)
    {
        _logger = logger;
        
        // Creates a new client
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(configuration["Mqtt:ClientId"])
            .WithTcpServer(configuration["Mqtt:Broker"],  configuration.GetValue<int>("Mqtt:Port"));

        // Create client options objects
        var options = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(60))
            .WithClientOptions(builder.Build())
            .Build();

        // Creates the client object
        _mqttClient = new MqttFactory().CreateManagedMqttClient();
        
        // Set up handlers
        _mqttClient.ConnectedAsync += MqttClientOnConnectedAsync;
        _mqttClient.DisconnectedAsync += MqttClientOnDisconnectedAsync;
        _mqttClient.ConnectingFailedAsync += MqttClientOnConnectingFailedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += MqttClientOnApplicationMessageReceivedAsync;

        // Starts a connection with the Broker
        _mqttClient.StartAsync(options).GetAwaiter().GetResult();
        
    }


    public bool IsConnected => _mqttClient.IsConnected;

    public async Task<bool> PublishAsync(string topic, string message)
    {
        if (_mqttClient.IsConnected)
        {
            await _mqttClient.EnqueueAsync(topic, message, MqttQualityOfServiceLevel.AtMostOnce, true);
            return true;
        }

        return false;
    }

    public async Task<bool> SubscribeAsync(string topic)
    {
        if (_mqttClient.IsConnected)
        {
            await _mqttClient.SubscribeAsync(topic);
        }
        else
        {
            _logger.LogDebug("Client not connected");
            return false;
        }
        
        return true;
    }
    
    public async Task<bool> UnsubscribeAsync(string topic)
    {
        if (_mqttClient.IsConnected)
        {
            await _mqttClient.UnsubscribeAsync(topic);
        }

        return true;
    }

    private Task MqttClientOnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        _logger.LogDebug("MQTT Message Received {Topic}", arg.ApplicationMessage.Topic);
        OnMessageReceived?.Invoke(this, arg);
        return Task.CompletedTask;
    }

    private Task MqttClientOnConnectingFailedAsync(ConnectingFailedEventArgs arg)
    {
        _logger.LogDebug("Couldn\'t connect to broker.{ArgException}", arg.Exception);
        return Task.CompletedTask;
    }

    private Task MqttClientOnDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
    {
        _logger.LogDebug("Successfully disconnected.");
        return Task.CompletedTask;
    }

    private Task MqttClientOnConnectedAsync(MqttClientConnectedEventArgs arg)
    {
        _logger.LogDebug("Successfully connected.");
        return Task.CompletedTask;
    }
}