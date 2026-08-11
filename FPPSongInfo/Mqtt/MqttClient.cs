using FPPSongInfo.Configuration;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;

namespace FPPSongInfo.Mqtt;

internal sealed class MqttClient : IMqttClient, IAsyncDisposable
{
    private readonly object _lifecycleLock = new();
    private readonly ILogger<MqttClient> _logger;
    private readonly IManagedMqttClient _mqttClient;
    private readonly ManagedMqttClientOptions _mqttClientOptions;
    private readonly MqttOptions _options;
    private readonly HashSet<string> _subscriptions = new(StringComparer.Ordinal);
    private bool _disposed;
    private bool _started;

    public MqttClient(IOptions<MqttOptions> mqttOptions, ILogger<MqttClient> logger)
    {
        ArgumentNullException.ThrowIfNull(mqttOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _options = mqttOptions.Value;

        _logger.LogInformation(
            "Initializing MQTT client for broker {Broker} on port {Port} with client ID {ClientId}",
            _options.Broker,
            _options.Port,
            _options.ClientId);

        var clientOptions = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithTcpServer(_options.Broker, _options.Port)
            .Build();

        _mqttClientOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(60))
            .WithClientOptions(clientOptions)
            .Build();

        _mqttClient = new MqttFactory().CreateManagedMqttClient();
        _mqttClient.ConnectedAsync += MqttClientOnConnectedAsync;
        _mqttClient.DisconnectedAsync += MqttClientOnDisconnectedAsync;
        _mqttClient.ConnectingFailedAsync += MqttClientOnConnectingFailedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += MqttClientOnApplicationMessageReceivedAsync;
    }

    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? MessageReceived;

    public bool IsConnected => _mqttClient.IsConnected;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("MQTT is disabled by configuration.");
            return;
        }

        lock (_lifecycleLock)
        {
            ThrowIfDisposed();

            if (_started)
            {
                return;
            }

            _started = true;
        }

        try
        {
            await _mqttClient.StartAsync(_mqttClientOptions).WaitAsync(cancellationToken);
        }
        catch
        {
            lock (_lifecycleLock)
            {
                _started = false;
            }

            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        bool shouldStop;
        lock (_lifecycleLock)
        {
            shouldStop = _started;
            _started = false;
        }

        if (shouldStop)
        {
            await _mqttClient.StopAsync(true).WaitAsync(cancellationToken);
        }
    }

    public async Task<bool> PublishAsync(string topic, string message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_mqttClient.IsConnected)
        {
            return false;
        }

        await _mqttClient.EnqueueAsync(topic, message, MqttQualityOfServiceLevel.AtMostOnce, true)
            .WaitAsync(cancellationToken);
        return true;
    }

    public async Task SubscribeAsync(string topic, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        cancellationToken.ThrowIfCancellationRequested();

        var subscribeNow = false;
        lock (_lifecycleLock)
        {
            ThrowIfDisposed();
            subscribeNow = _subscriptions.Add(topic) && _mqttClient.IsConnected;
        }

        if (subscribeNow)
        {
            await SubscribeCoreAsync(topic, cancellationToken);
        }
    }

    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        cancellationToken.ThrowIfCancellationRequested();

        var unsubscribeNow = false;
        lock (_lifecycleLock)
        {
            ThrowIfDisposed();
            unsubscribeNow = _subscriptions.Remove(topic) && _mqttClient.IsConnected;
        }

        if (unsubscribeNow)
        {
            await _mqttClient.UnsubscribeAsync(topic).WaitAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        try
        {
            await StopAsync(CancellationToken.None);
        }
        finally
        {
            _mqttClient.ConnectedAsync -= MqttClientOnConnectedAsync;
            _mqttClient.DisconnectedAsync -= MqttClientOnDisconnectedAsync;
            _mqttClient.ConnectingFailedAsync -= MqttClientOnConnectingFailedAsync;
            _mqttClient.ApplicationMessageReceivedAsync -= MqttClientOnApplicationMessageReceivedAsync;
            _mqttClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private async Task SubscribeCoreAsync(string topic, CancellationToken cancellationToken) =>
        await _mqttClient.SubscribeAsync(topic).WaitAsync(cancellationToken);

    private async Task MqttClientOnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arguments)
    {
        _logger.LogDebug("MQTT message received on topic {Topic}", arguments.ApplicationMessage.Topic);

        var handlers = MessageReceived;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<MqttApplicationMessageReceivedEventArgs, Task>>())
        {
            try
            {
                await handler(arguments);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "MQTT message handler failed for topic {Topic}", arguments.ApplicationMessage.Topic);
            }
        }
    }

    private async Task MqttClientOnConnectedAsync(MqttClientConnectedEventArgs arguments)
    {
        _logger.LogDebug("Successfully connected to MQTT broker.");

        string[] subscriptions;
        lock (_lifecycleLock)
        {
            subscriptions = _subscriptions.ToArray();
        }

        foreach (var topic in subscriptions)
        {
            try
            {
                await SubscribeCoreAsync(topic, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not restore MQTT subscription for topic {Topic}", topic);
            }
        }
    }

    private Task MqttClientOnConnectingFailedAsync(ConnectingFailedEventArgs arguments)
    {
        _logger.LogError(arguments.Exception, "Could not connect to MQTT broker.");
        return Task.CompletedTask;
    }

    private Task MqttClientOnDisconnectedAsync(MqttClientDisconnectedEventArgs arguments)
    {
        _logger.LogDebug("Disconnected from MQTT broker.");
        return Task.CompletedTask;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
