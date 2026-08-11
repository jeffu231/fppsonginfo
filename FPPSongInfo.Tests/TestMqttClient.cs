using MQTTnet.Client;
using IMqttClient = FPPSongInfo.Mqtt.IMqttClient;

namespace FPPSongInfo.Tests;

internal sealed class TestMqttClient : IMqttClient
{
    private Func<MqttApplicationMessageReceivedEventArgs, Task>? _messageReceived;
    private readonly TaskCompletionSource _subscribed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? MessageReceived
    {
        add => _messageReceived += value;
        remove => _messageReceived -= value;
    }

    public int HandlerCount => _messageReceived?.GetInvocationList().Length ?? 0;
    public bool IsConnected => true;
    public List<string> Subscriptions { get; } = [];
    public List<string> Unsubscriptions { get; } = [];

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<bool> PublishAsync(string topic, string message, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task SubscribeAsync(string topic, CancellationToken cancellationToken)
    {
        Subscriptions.Add(topic);
        _subscribed.TrySetResult();
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string topic, CancellationToken cancellationToken)
    {
        Unsubscriptions.Add(topic);
        return Task.CompletedTask;
    }

    public async Task PublishMessageAsync(MqttApplicationMessageReceivedEventArgs message)
    {
        var handlers = _messageReceived;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<MqttApplicationMessageReceivedEventArgs, Task>>())
        {
            await handler(message);
        }
    }

    public Task WaitForSubscriptionAsync() => _subscribed.Task;
}
