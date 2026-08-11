using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using FPPSongInfo.Configuration;
using Microsoft.Extensions.Options;
using MQTTnet.Client;
using IMqttClient = FPPSongInfo.Mqtt.IMqttClient;

namespace FPPSongInfo.Service;

internal sealed class RadioAutomationConsumerService(
    IMqttClient mqttClient,
    IOptions<MqttOptions> mqttOptions,
    IOptions<RadioAutomationOptions> radioAutomationOptions,
    ILogger<RadioAutomationConsumerService> logger,
    ISongInfoWriter songInfoWriter)
    : BackgroundService
{
    private readonly ILogger<RadioAutomationConsumerService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private ChannelWriter<MqttApplicationMessageReceivedEventArgs>? _messageWriter;
    private readonly IMqttClient _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
    private readonly MqttOptions _mqttOptions = mqttOptions?.Value ?? throw new ArgumentNullException(nameof(mqttOptions));
    private readonly RadioAutomationOptions _radioAutomationOptions = radioAutomationOptions?.Value ?? throw new ArgumentNullException(nameof(radioAutomationOptions));
    private readonly ISongInfoWriter _songInfoWriter = songInfoWriter ?? throw new ArgumentNullException(nameof(songInfoWriter));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var songInfoTopic = _mqttOptions.RootTopic + _radioAutomationOptions.SongTopic;
        var subscriptionTopic = songInfoTopic + "/#";
        var channel = Channel.CreateUnbounded<MqttApplicationMessageReceivedEventArgs>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false
            });

        Volatile.Write(ref _messageWriter, channel.Writer);
        _mqttClient.MessageReceived += OnMessageReceivedAsync;

        try
        {
            _logger.LogDebug("Subscribing to radio automation topic {Topic}", subscriptionTopic);
            await _mqttClient.SubscribeAsync(subscriptionTopic, stoppingToken);

            await foreach (var message in channel.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessMessageAsync(message, songInfoTopic, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Radio automation consumer cancellation requested.");
        }
        finally
        {
            _mqttClient.MessageReceived -= OnMessageReceivedAsync;
            Volatile.Write(ref _messageWriter, null);
            channel.Writer.TryComplete();

            try
            {
                await _mqttClient.UnsubscribeAsync(subscriptionTopic, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not unsubscribe from radio automation topic {Topic}", subscriptionTopic);
            }
        }
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs message)
    {
        var messageWriter = Volatile.Read(ref _messageWriter);
        if (messageWriter is not null && !messageWriter.TryWrite(message))
        {
            _logger.LogDebug("Ignoring radio automation message because the consumer is stopping.");
        }

        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(
        MqttApplicationMessageReceivedEventArgs message,
        string songInfoTopic,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(message.ApplicationMessage.Topic, songInfoTopic, StringComparison.Ordinal))
        {
            return;
        }

        var payload = Encoding.UTF8.GetString(message.ApplicationMessage.PayloadSegment);
        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogWarning("Ignoring empty radio automation payload on topic {Topic}", songInfoTopic);
            return;
        }

        SongInfo? songInfo;
        try
        {
            songInfo = JsonSerializer.Deserialize<SongInfo>(payload);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Ignoring malformed radio automation payload on topic {Topic}", songInfoTopic);
            return;
        }

        if (songInfo is null)
        {
            _logger.LogWarning("Ignoring null radio automation payload on topic {Topic}", songInfoTopic);
            return;
        }

        await _songInfoWriter.UpdateSongInfoAsync(songInfo, cancellationToken);
        _logger.LogDebug("Updated radio automation song info from topic {Topic}", songInfoTopic);
    }
}
