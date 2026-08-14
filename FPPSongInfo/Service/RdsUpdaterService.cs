using System.Threading.Channels;
using FPPSongInfo.Configuration;
using FPPSongInfo.Rds;
using Microsoft.Extensions.Options;

namespace FPPSongInfo.Service;

internal sealed class RdsUpdaterService(
    IMrds192Bus bus,
    IMrds192DeviceClient deviceClient,
    IOptions<RdsOptions> rdsOptions,
    TimeProvider timeProvider,
    ILogger<RdsUpdaterService> logger) : BackgroundService, ISongInfoSink
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    private readonly IMrds192Bus _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    private readonly IMrds192DeviceClient _deviceClient = deviceClient ?? throw new ArgumentNullException(nameof(deviceClient));
    private readonly ILogger<RdsUpdaterService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly RdsOptions _rdsOptions = rdsOptions?.Value ?? throw new ArgumentNullException(nameof(rdsOptions));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly Channel<SongInfo> _songUpdates = Channel.CreateBounded<SongInfo>(
        new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    private SongInfo? _currentSong;
    private int _currentSlotIndex;
    private string _failedOperation = "initialization";
    private string _failedRegister = "multiple";

    public Task UpdateAsync(SongInfo songInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(songInfo);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(songInfo.Artist) || string.IsNullOrWhiteSpace(songInfo.Title))
        {
            return Task.CompletedTask;
        }

        _songUpdates.Writer.TryWrite(new SongInfo(songInfo.Artist.Trim(), songInfo.Title.Trim()));
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryAttempt = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _failedOperation = "initialization";
                    _failedRegister = "multiple";
                    await _deviceClient.InitializeAsync(stoppingToken);
                    retryAttempt = 0;
                    _logger.LogInformation("RDS initialized successfully on port {PortName}", _rdsOptions.PortName);
                    DrainSongUpdates();

                    if (_currentSong is not null)
                    {
                        await SendCurrentSongRadioTextAsync(stoppingToken);
                    }

                    await ProcessConnectedEventsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    var retryDelay = RetryDelays[Math.Min(retryAttempt, RetryDelays.Length - 1)];
                    retryAttempt++;
                    CloseBusAfterFailure();
                    _logger.LogWarning(
                        exception,
                        "RDS operation {Operation} for register {Register} failed on port {PortName}; retrying in {RetryDelay}",
                        _failedOperation,
                        _failedRegister,
                        _rdsOptions.PortName,
                        retryDelay);
                    await WaitForRetryAsync(retryDelay, stoppingToken);
                }
            }
        }
        finally
        {
            CloseBusAfterFailure();
        }
    }

    private void CloseBusAfterFailure()
    {
        try
        {
            _bus.Close();
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Could not close RDS bus on port {PortName}", _rdsOptions.PortName);
        }
    }

    private void DrainSongUpdates()
    {
        while (_songUpdates.Reader.TryRead(out var songInfo))
        {
            _currentSong = songInfo;
            _currentSlotIndex = 0;
        }
    }

    private IReadOnlyList<string> GetRadioTextSlots()
    {
        if (_currentSong is null)
        {
            return [];
        }

        var slots = new List<string> { $"{_currentSong.Artist} - {_currentSong.Title}" };
        slots.AddRange(_rdsOptions.AdditionalRadioTextMessages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => message.Trim()));
        return slots;
    }

    private async Task ProcessConnectedEventsAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset? nextRotation = _currentSong is null
            ? null
            : _timeProvider.GetUtcNow() + _rdsOptions.RadioTextRotationInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            var songArrived = await WaitForSongOrRotationAsync(nextRotation, stoppingToken);
            if (songArrived)
            {
                DrainSongUpdates();
                if (_currentSong is not null)
                {
                    await SendCurrentSongRadioTextAsync(stoppingToken);
                    nextRotation = _timeProvider.GetUtcNow() + _rdsOptions.RadioTextRotationInterval;
                }

                continue;
            }

            var slots = GetRadioTextSlots();
            if (slots.Count == 0)
            {
                nextRotation = null;
                continue;
            }

            _currentSlotIndex = (_currentSlotIndex + 1) % slots.Count;
            await SendRadioTextAsync(slots[_currentSlotIndex], stoppingToken);
            nextRotation = _timeProvider.GetUtcNow() + _rdsOptions.RadioTextRotationInterval;
        }
    }

    private async Task SendCurrentSongRadioTextAsync(CancellationToken cancellationToken)
    {
        var slots = GetRadioTextSlots();
        if (slots.Count == 0)
        {
            return;
        }

        _currentSlotIndex = 0;
        await SendRadioTextAsync(slots[0], cancellationToken);
    }

    private async Task SendRadioTextAsync(string radioText, CancellationToken cancellationToken)
    {
        _failedOperation = "RadioText update";
        _failedRegister = "0x20/0x1F";
        await _deviceClient.WriteRadioTextAsync(radioText, cancellationToken);
        _logger.LogDebug(
            "RDS RadioText A/B toggle committed on port {PortName} for slot {SlotIndex}: {RadioText}",
            _rdsOptions.PortName,
            _currentSlotIndex,
            radioText);
    }

    private async Task WaitForRetryAsync(TimeSpan retryDelay, CancellationToken stoppingToken)
    {
        var retryTask = Task.Delay(retryDelay, _timeProvider, stoppingToken);
        while (!retryTask.IsCompleted)
        {
            var songAvailableTask = _songUpdates.Reader.WaitToReadAsync(stoppingToken).AsTask();
            var completedTask = await Task.WhenAny(songAvailableTask, retryTask);
            if (completedTask == retryTask)
            {
                await retryTask;
                return;
            }

            if (await songAvailableTask)
            {
                DrainSongUpdates();
            }
        }
    }

    private async Task<bool> WaitForSongOrRotationAsync(
        DateTimeOffset? nextRotation,
        CancellationToken stoppingToken)
    {
        if (nextRotation is null)
        {
            return await _songUpdates.Reader.WaitToReadAsync(stoppingToken);
        }

        using var songWaitCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var songAvailableTask = _songUpdates.Reader.WaitToReadAsync(songWaitCancellation.Token).AsTask();
        var delay = nextRotation.Value - _timeProvider.GetUtcNow();
        var rotationTask = Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, _timeProvider, stoppingToken);
        var completedTask = await Task.WhenAny(songAvailableTask, rotationTask);

        if (completedTask == songAvailableTask)
        {
            return await songAvailableTask;
        }

        songWaitCancellation.Cancel();
        try
        {
            await songAvailableTask;
        }
        catch (OperationCanceledException) when (songWaitCancellation.IsCancellationRequested)
        {
        }

        await rotationTask;
        return false;
    }
}
