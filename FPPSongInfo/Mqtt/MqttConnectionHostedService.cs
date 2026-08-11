namespace FPPSongInfo.Mqtt;

internal sealed class MqttConnectionHostedService(IMqttClient mqttClient) : IHostedService
{
    private readonly IMqttClient _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _mqttClient.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _mqttClient.StopAsync(cancellationToken);
        }
        finally
        {
            await _mqttClient.DisposeAsync();
        }
    }
}
