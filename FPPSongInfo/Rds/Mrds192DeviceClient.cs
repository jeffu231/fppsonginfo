using FPPSongInfo.Configuration;
using Microsoft.Extensions.Options;

namespace FPPSongInfo.Rds;

internal sealed class Mrds192DeviceClient : IMrds192DeviceClient
{
    private const byte DynamicProgramServiceAddress = 0x77;
    private const byte DynamicProgramServiceLengthAddress = 0x76;
    private const byte DynamicProgramServiceModeAddress = 0x73;
    private const byte LabelPeriodAddress = 0x74;
    private const byte LoopDelayAddress = 0x72;
    private const byte RadioTextAddress = 0x20;
    private const byte RadioTextEnableAddress = 0x1f;
    private const byte RadioTextEnabled = 0x01;
    private const byte RadioTextTypeB = 0x02;
    private const byte ScrollingSpeedAddress = 0x75;
    private const byte StaticProgramServiceAddress = 0x02;
    private const byte StatusAddress = 0x70;
    internal static readonly TimeSpan BufferedProgramServiceWriteDelay = TimeSpan.FromMilliseconds(620);
    private readonly IMrds192Bus _bus;
    private readonly TimeSpan _bufferedProgramServiceWriteDelay;
    private readonly RdsOptions _options;
    private bool _lastSuccessfullyWrittenTypeB;

    public Mrds192DeviceClient(IMrds192Bus bus, IOptions<RdsOptions> rdsOptions)
        : this(bus, rdsOptions?.Value ?? throw new ArgumentNullException(nameof(rdsOptions)))
    {
    }

    internal Mrds192DeviceClient(IMrds192Bus bus, RdsOptions options)
        : this(bus, options, BufferedProgramServiceWriteDelay)
    {
    }

    internal Mrds192DeviceClient(
        IMrds192Bus bus,
        RdsOptions options,
        TimeSpan bufferedProgramServiceWriteDelay)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _bufferedProgramServiceWriteDelay = bufferedProgramServiceWriteDelay;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _bus.Open();

        await ReadRegisterAsync(StatusAddress, cancellationToken);
        var radioTextEnable = await ReadRegisterAsync(RadioTextEnableAddress, cancellationToken);
        _lastSuccessfullyWrittenTypeB = (radioTextEnable & RadioTextTypeB) != 0;

        await _bus.WriteAsync(
            StaticProgramServiceAddress,
            RdsTextEncoder.EncodeStaticProgramService(_options.StaticProgramService),
            cancellationToken);
        await Task.Delay(_bufferedProgramServiceWriteDelay, cancellationToken);

        await _bus.WriteAsync(
            LoopDelayAddress,
            new byte[] { RdsTimingConverter.ToLoopDelayRaw(_options.LoopDelay) },
            cancellationToken);
        await _bus.WriteAsync(
            DynamicProgramServiceModeAddress,
            new byte[] { (byte)_options.DynamicPsMode },
            cancellationToken);
        await _bus.WriteAsync(
            LabelPeriodAddress,
            new byte[] { RdsTimingConverter.ToLabelPeriodRaw(_options.LabelPeriod) },
            cancellationToken);
        await _bus.WriteAsync(ScrollingSpeedAddress, new byte[] { 0 }, cancellationToken);
        await WriteDynamicProgramServiceAsync(cancellationToken);
        await _bus.WriteAsync(
            RadioTextEnableAddress,
            new byte[] { CreateRadioTextEnableValue(_lastSuccessfullyWrittenTypeB) },
            cancellationToken);
    }

    public async Task WriteRadioTextAsync(string radioText, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(radioText);

        var nextTypeB = !_lastSuccessfullyWrittenTypeB;
        await _bus.WriteAsync(
            RadioTextAddress,
            RdsTextEncoder.EncodeRadioText(radioText),
            cancellationToken);
        await _bus.WriteAsync(
            RadioTextEnableAddress,
            new byte[] { CreateRadioTextEnableValue(nextTypeB) },
            cancellationToken);
        _lastSuccessfullyWrittenTypeB = nextTypeB;
    }

    private byte CreateRadioTextEnableValue(bool typeB) =>
        (byte)(RadioTextEnabled | (typeB ? RadioTextTypeB : 0));

    private async Task<byte> ReadRegisterAsync(byte registerAddress, CancellationToken cancellationToken)
    {
        var result = await _bus.ReadAsync(registerAddress, 1, cancellationToken);
        if (result.Length != 1)
        {
            throw new IOException($"MRDS192 returned an invalid response for register 0x{registerAddress:X2}.");
        }

        return result[0];
    }

    private async Task WriteDynamicProgramServiceAsync(CancellationToken cancellationToken)
    {
        var dynamicProgramService = RdsTextEncoder.EncodeDynamicProgramService(_options.DynamicProgramService);
        await _bus.WriteAsync(DynamicProgramServiceLengthAddress, new byte[] { 0 }, cancellationToken);
        await _bus.WriteAsync(DynamicProgramServiceAddress, dynamicProgramService, cancellationToken);
        await _bus.WriteAsync(
            DynamicProgramServiceLengthAddress,
            new byte[] { checked((byte)dynamicProgramService.Length) },
            cancellationToken);
    }
}
