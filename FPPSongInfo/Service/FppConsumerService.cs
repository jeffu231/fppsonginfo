using System.Text;
using System.Threading.Channels;
using FPPSongInfo.Configuration;
using Microsoft.Extensions.Options;
using MQTTnet.Client;
using IMqttClient = FPPSongInfo.Mqtt.IMqttClient;

namespace FPPSongInfo.Service;

internal sealed class FppConsumerService(
    IMqttClient mqttClient,
    IOptions<MqttOptions> mqttOptions,
    IOptions<FppOptions> fppOptions,
    ILogger<FppConsumerService> logger,
    ISongInfoWriter songInfoWriter)
    : BackgroundService
{
    private readonly FppOptions _fppOptions = fppOptions?.Value ?? throw new ArgumentNullException(nameof(fppOptions));
    private readonly ILogger<FppConsumerService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IMqttClient _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
    private readonly MqttOptions _mqttOptions = mqttOptions?.Value ?? throw new ArgumentNullException(nameof(mqttOptions));
    private readonly ISongInfoWriter _songInfoWriter = songInfoWriter ?? throw new ArgumentNullException(nameof(songInfoWriter));
    private string _artist = string.Empty;
    private ChannelWriter<MqttApplicationMessageReceivedEventArgs>? _messageWriter;
    private string _title = string.Empty;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var songTopic = _mqttOptions.RootTopic + _fppOptions.SongTopic;
        var subscriptionTopic = songTopic + "/#";
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
            _logger.LogDebug("Subscribing to FPP topic {Topic}", subscriptionTopic);
            await _mqttClient.SubscribeAsync(subscriptionTopic, stoppingToken);

            await foreach (var message in channel.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessMessageAsync(message, songTopic);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("FPP consumer cancellation requested.");
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
                _logger.LogError(exception, "Could not unsubscribe from FPP topic {Topic}", subscriptionTopic);
            }
        }
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs message)
    {
        var messageWriter = Volatile.Read(ref _messageWriter);
        if (messageWriter is not null && !messageWriter.TryWrite(message))
        {
            _logger.LogDebug("Ignoring FPP message because the consumer is stopping.");
        }

        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(MqttApplicationMessageReceivedEventArgs message, string songTopic)
    {
        var topic = message.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(message.ApplicationMessage.PayloadSegment);

        if (string.Equals(topic, songTopic + "/artist", StringComparison.Ordinal))
        {
            _artist = payload;
        }
        else if (string.Equals(topic, songTopic + "/title", StringComparison.Ordinal))
        {
            _title = payload;
        }
        else
        {
            return;
        }

        await _songInfoWriter.UpdateSongInfo(_artist, _title);
        _logger.LogDebug("Updated FPP song info from topic {Topic}", topic);
    }
}
