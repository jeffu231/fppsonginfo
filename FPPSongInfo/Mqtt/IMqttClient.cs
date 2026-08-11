using MQTTnet.Client;

namespace FPPSongInfo.Mqtt;

public interface IMqttClient : IAsyncDisposable
{
    bool IsConnected { get; }
    Task<bool> PublishAsync(string topic, string message, CancellationToken cancellationToken);
    
    Task StartAsync(CancellationToken cancellationToken);
    
    Task StopAsync(CancellationToken cancellationToken);
    
    Task SubscribeAsync(string topic, CancellationToken cancellationToken);

    Task UnsubscribeAsync(string topic, CancellationToken cancellationToken);

    event Func<MqttApplicationMessageReceivedEventArgs, Task>? MessageReceived;
    
}
