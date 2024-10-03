using MQTTnet.Client;

namespace FPPSongInfo.Mqtt;

public interface IMqttClient
{
    bool IsConnected { get; }
    Task<bool> PublishAsync(string topic, string message);
    
    Task<bool> SubscribeAsync(string topic);
    
    Task<bool> UnsubscribeAsync(string topic);
    
    event EventHandler<MqttApplicationMessageReceivedEventArgs> OnMessageReceived;
    
}