using System.Diagnostics;
using System.IO.Ports;
using FPPSongInfo.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPPSongInfo.Rds;

internal sealed class SerialControlLineMrds192Bus : IMrds192Bus
{
    private const byte ReadDeviceAddress = 0xd7;
    private const byte WriteDeviceAddress = 0xd6;
    private readonly bool _ctsHighIsSdaHigh;
    private readonly bool _dtrEnabledIsSdaHigh;
    private readonly ILogger<SerialControlLineMrds192Bus> _logger;
    private readonly IModemControlLines _modemControlLines;
    private readonly string _portName;
    private readonly bool _rtsEnabledIsSclHigh;
    private readonly Mrds192ControlLineTransport _transport;
    private readonly SemaphoreSlim _transactionLock = new(1, 1);
    private readonly Mrds192BusTiming _timing;
    private bool _disposed;

    public SerialControlLineMrds192Bus(
        IOptions<RdsOptions> rdsOptions,
        ILogger<SerialControlLineMrds192Bus> logger)
        : this(
            rdsOptions?.Value ?? throw new ArgumentNullException(nameof(rdsOptions)),
            logger ?? throw new ArgumentNullException(nameof(logger)))
    {
    }

    internal SerialControlLineMrds192Bus(RdsOptions options, ILogger<SerialControlLineMrds192Bus>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _portName = options.PortName;
        _dtrEnabledIsSdaHigh = options.DtrEnabledIsSdaHigh;
        _rtsEnabledIsSclHigh = options.RtsEnabledIsSclHigh;
        _ctsHighIsSdaHigh = options.CtsHighIsSdaHigh;
        _transport = options.ControlLineTransport;
        _logger = logger ?? NullLogger<SerialControlLineMrds192Bus>.Instance;
        _modemControlLines = CreateModemControlLines(options);
        _timing = Mrds192BusTiming.Create(options.Slow);
    }

    internal SerialControlLineMrds192Bus(
        IModemControlLines modemControlLines,
        Mrds192BusTiming timing,
        bool dtrEnabledIsSdaHigh = true,
        bool rtsEnabledIsSclHigh = true,
        bool ctsHighIsSdaHigh = true)
    {
        _modemControlLines = modemControlLines ?? throw new ArgumentNullException(nameof(modemControlLines));
        _timing = timing;
        _portName = "test";
        _dtrEnabledIsSdaHigh = dtrEnabledIsSdaHigh;
        _rtsEnabledIsSclHigh = rtsEnabledIsSclHigh;
        _ctsHighIsSdaHigh = ctsHighIsSdaHigh;
        _transport = Mrds192ControlLineTransport.SerialPort;
        _logger = NullLogger<SerialControlLineMrds192Bus>.Instance;
    }

    public void Close()
    {
        ThrowIfDisposed();

        if (!_modemControlLines.IsOpen)
        {
            return;
        }

        SetSdaHigh();
        SetSclHigh();
        _modemControlLines.Close();
    }

    public void Open()
    {
        ThrowIfDisposed();

        if (!_modemControlLines.IsOpen)
        {
            _modemControlLines.Open();
        }

        SetSdaHigh();
        SetSclHigh();
        ProbeControlLines();
        _logger.LogInformation(
            "Opened MRDS192 I2C bus on port {PortName} using {Transport} with DTR asserted is SDA high {DtrEnabledIsSdaHigh}, RTS asserted is SCL high {RtsEnabledIsSclHigh}, and CTS high is SDA high {CtsHighIsSdaHigh}",
            _portName,
            _transport,
            _dtrEnabledIsSdaHigh,
            _rtsEnabledIsSclHigh,
            _ctsHighIsSdaHigh);
    }

    public async Task<byte[]> ReadAsync(byte registerAddress, int length, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        cancellationToken.ThrowIfCancellationRequested();

        if (length == 0)
        {
            return [];
        }

        await _transactionLock.WaitAsync(cancellationToken);
        var started = false;
        try
        {
            EnsureOpen();
            await SendStartAsync(cancellationToken);
            started = true;
            await WriteByteAsync(WriteDeviceAddress, "device write address", cancellationToken);
            await WriteByteAsync(registerAddress, "register address", cancellationToken);
            await SendStartAsync(cancellationToken);
            await WriteByteAsync(ReadDeviceAddress, "device read address", cancellationToken);

            var data = new byte[length];
            for (var index = 0; index < data.Length; index++)
            {
                data[index] = await ReadByteAsync(index < data.Length - 1, cancellationToken);
            }

            await SendStopAsync(cancellationToken);
            started = false;
            return data;
        }
        finally
        {
            if (started)
            {
                RestoreIdleBusState();
            }

            _transactionLock.Release();
        }
    }

    public async Task WriteAsync(byte registerAddress, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _transactionLock.WaitAsync(cancellationToken);
        var started = false;
        try
        {
            EnsureOpen();
            await SendStartAsync(cancellationToken);
            started = true;
            await WriteByteAsync(WriteDeviceAddress, "device write address", cancellationToken);
            await WriteByteAsync(registerAddress, "register address", cancellationToken);

            for (var index = 0; index < data.Length; index++)
            {
                await WriteByteAsync(data.Span[index], "register data", cancellationToken);
            }

            await SendStopAsync(cancellationToken);
            started = false;
        }
        finally
        {
            if (started)
            {
                RestoreIdleBusState();
            }

            _transactionLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_modemControlLines.IsOpen)
        {
            RestoreIdleBusState();
            _modemControlLines.Close();
        }

        _modemControlLines.Dispose();
        _transactionLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task ClockHighAsync(CancellationToken cancellationToken)
    {
        SetSclHigh();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
    }

    private async Task ClockLowAsync(CancellationToken cancellationToken)
    {
        SetSclLow();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
    }

    private async Task<byte> ReadByteAsync(bool sendAcknowledge, CancellationToken cancellationToken)
    {
        var value = 0;
        SetSdaHigh();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);

        for (var bit = 7; bit >= 0; bit--)
        {
            await ClockHighAsync(cancellationToken);
            if (ReadSda())
            {
                value |= 1 << bit;
            }

            await ClockLowAsync(cancellationToken);
        }

        SetSda(sendAcknowledge ? false : true);
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
        await ClockHighAsync(cancellationToken);
        await ClockLowAsync(cancellationToken);
        SetSdaHigh();
        return (byte)value;
    }

    private bool ReadSda() => _modemControlLines.ClearToSend == _ctsHighIsSdaHigh;

    private void ProbeControlLines()
    {
        // Keep SCL low throughout the probe so no I2C START or STOP condition is generated.
        SetSclLow();
        SetSdaHigh();
        Thread.Sleep(_timing.ClockTransitionDelay);
        var ctsWhenSdaReleased = _modemControlLines.ClearToSend;

        SetSdaLow();
        Thread.Sleep(_timing.ClockTransitionDelay);
        var ctsWhenSdaDrivenLow = _modemControlLines.ClearToSend;

        SetSdaHigh();
        SetSclHigh();
        _logger.LogInformation(
            "MRDS192 COM-line probe on port {PortName}: CTS is {CtsWhenSdaReleased} with SDA released and {CtsWhenSdaDrivenLow} with SDA driven low; CTS high is configured as SDA high {CtsHighIsSdaHigh}",
            _portName,
            ctsWhenSdaReleased,
            ctsWhenSdaDrivenLow,
            _ctsHighIsSdaHigh);
    }

    private async Task SendStartAsync(CancellationToken cancellationToken)
    {
        SetSdaHigh();
        SetSclHigh();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
        SetSdaLow();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
        SetSclLow();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
    }

    private async Task SendStopAsync(CancellationToken cancellationToken)
    {
        SetSdaLow();
        SetSclLow();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
        SetSclHigh();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
        SetSdaHigh();
        await DelayAsync(_timing.BusFreeDelay, cancellationToken);
    }

    private async Task WriteByteAsync(byte value, string phase, CancellationToken cancellationToken)
    {
        for (var bit = 7; bit >= 0; bit--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetSda((value & (1 << bit)) != 0);
            await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
            await ClockHighAsync(cancellationToken);
            await ClockLowAsync(cancellationToken);
        }

        SetSdaHigh();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
        await ClockHighAsync(cancellationToken);
        var ctsHolding = _modemControlLines.ClearToSend;
        var acknowledged = ctsHolding != _ctsHighIsSdaHigh;
        await ClockLowAsync(cancellationToken);

        if (!acknowledged)
        {
            _logger.LogWarning(
                "MRDS192 I2C NACK on port {PortName} for {Phase} byte 0x{Byte:X2}; CTS is {CtsHolding} and CTS high is configured as SDA high {CtsHighIsSdaHigh}",
                _portName,
                phase,
                value,
                ctsHolding,
                _ctsHighIsSdaHigh);
            throw new IOException("MRDS192 did not acknowledge an I2C byte.");
        }
    }

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (delay <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        var startedAt = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(startedAt) < delay)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Thread.SpinWait(64);
        }

        return Task.CompletedTask;
    }

    private static IModemControlLines CreateModemControlLines(RdsOptions options) => options.ControlLineTransport switch
    {
        Mrds192ControlLineTransport.SerialPort => new SerialPortModemControlLines(new SerialPort(
            options.PortName,
            options.Slow ? 2400 : 19200,
            Parity.None,
            dataBits: 8,
            StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = !options.DtrEnabledIsSdaHigh,
            RtsEnable = !options.RtsEnabledIsSclHigh
        }),
        Mrds192ControlLineTransport.Win32 => new Win32ModemControlLines(options.PortName),
        _ => throw new ArgumentOutOfRangeException(nameof(options.ControlLineTransport))
    };

    private void EnsureOpen()
    {
        ThrowIfDisposed();
        if (!_modemControlLines.IsOpen)
        {
            throw new InvalidOperationException("The MRDS192 bus must be opened before it is used.");
        }
    }

    private void RestoreIdleBusState()
    {
        SetSdaHigh();
        SetSclHigh();
    }

    private void SetScl(bool high) => _modemControlLines.RequestToSend = high == _rtsEnabledIsSclHigh;

    private void SetSclHigh() => SetScl(true);

    private void SetSclLow() => SetScl(false);

    private void SetSda(bool high) => _modemControlLines.DataTerminalReady = high == _dtrEnabledIsSdaHigh;

    private void SetSdaHigh() => SetSda(true);

    private void SetSdaLow() => SetSda(false);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
