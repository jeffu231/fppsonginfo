using System.IO.Ports;
using FPPSongInfo.Configuration;
using Microsoft.Extensions.Options;

namespace FPPSongInfo.Rds;

internal sealed class SerialControlLineMrds192Bus : IMrds192Bus
{
    private const bool CtsHighIsSdaHigh = true;
    private const bool DtrEnabledIsSdaHigh = true;
    private const byte ReadDeviceAddress = 0xd7;
    private const bool RtsEnabledIsSclHigh = true;
    private const byte WriteDeviceAddress = 0xd6;
    private readonly IModemControlLines _modemControlLines;
    private readonly SemaphoreSlim _transactionLock = new(1, 1);
    private readonly Mrds192BusTiming _timing;
    private bool _disposed;

    public SerialControlLineMrds192Bus(IOptions<RdsOptions> rdsOptions)
        : this(rdsOptions?.Value ?? throw new ArgumentNullException(nameof(rdsOptions)))
    {
    }

    internal SerialControlLineMrds192Bus(RdsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var serialPort = new SerialPort(
            options.PortName,
            options.Slow ? 2400 : 19200,
            Parity.None,
            dataBits: 8,
            StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = !DtrEnabledIsSdaHigh,
            RtsEnable = !RtsEnabledIsSclHigh
        };

        _modemControlLines = new SerialPortModemControlLines(serialPort);
        _timing = Mrds192BusTiming.Create(options.Slow);
    }

    internal SerialControlLineMrds192Bus(IModemControlLines modemControlLines, Mrds192BusTiming timing)
    {
        _modemControlLines = modemControlLines ?? throw new ArgumentNullException(nameof(modemControlLines));
        _timing = timing;
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
            await WriteByteAsync(WriteDeviceAddress, cancellationToken);
            await WriteByteAsync(registerAddress, cancellationToken);
            await SendStartAsync(cancellationToken);
            await WriteByteAsync(ReadDeviceAddress, cancellationToken);

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
            await WriteByteAsync(WriteDeviceAddress, cancellationToken);
            await WriteByteAsync(registerAddress, cancellationToken);

            for (var index = 0; index < data.Length; index++)
            {
                await WriteByteAsync(data.Span[index], cancellationToken);
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

    private async Task ReadClockPulseAsync(CancellationToken cancellationToken)
    {
        SetSclLow();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
        SetSclHigh();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
    }

    private async Task<byte> ReadByteAsync(bool sendAcknowledge, CancellationToken cancellationToken)
    {
        var value = 0;
        SetSdaHigh();

        for (var bit = 7; bit >= 0; bit--)
        {
            await ReadClockPulseAsync(cancellationToken);
            if (ReadSda())
            {
                value |= 1 << bit;
            }
        }

        SetSda(sendAcknowledge ? false : true);
        await ReadClockPulseAsync(cancellationToken);
        SetSdaHigh();
        return (byte)value;
    }

    private bool ReadSda() => _modemControlLines.ClearToSend == CtsHighIsSdaHigh;

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

    private async Task WriteByteAsync(byte value, CancellationToken cancellationToken)
    {
        for (var bit = 7; bit >= 0; bit--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetSda((value & (1 << bit)) != 0);
            await ReadClockPulseAsync(cancellationToken);
        }

        SetSdaHigh();
        SetSclLow();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
        SetSclHigh();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);
        var acknowledged = !ReadSda();
        SetSclLow();
        await DelayAsync(_timing.ClockTransitionDelay, cancellationToken);

        if (!acknowledged)
        {
            throw new IOException("MRDS192 did not acknowledge an I2C byte.");
        }
    }

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);

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

    private void SetScl(bool high) => _modemControlLines.RequestToSend = high == RtsEnabledIsSclHigh;

    private void SetSclHigh() => SetScl(true);

    private void SetSclLow() => SetScl(false);

    private void SetSda(bool high) => _modemControlLines.DataTerminalReady = high == DtrEnabledIsSdaHigh;

    private void SetSdaHigh() => SetSda(true);

    private void SetSdaLow() => SetSda(false);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
