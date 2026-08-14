using FPPSongInfo.Rds;

namespace FPPSongInfo.Tests;

public sealed class SerialControlLineMrds192BusTests
{
    [Fact]
    public async Task WritesDeviceAddressRegisterAndDataAfterStartAsync()
    {
        var lines = new FakeModemControlLines([false, false, false, false, false]);
        using var bus = CreateBus(lines);
        bus.Open();

        await bus.WriteAsync(0x20, new byte[] { 0xab }, CancellationToken.None);

        Assert.True(lines.IsOpen);
        Assert.True(ContainsBitSequence(lines.ClockRisingDataValues, GetBits(0xd6)));
        Assert.True(ContainsBitSequence(lines.ClockRisingDataValues, GetBits(0x20)));
        Assert.True(ContainsBitSequence(lines.ClockRisingDataValues, GetBits(0xab)));
        Assert.Equal(2, lines.DataTransitionsWhileClockHigh);
        Assert.True(lines.DataTerminalReady);
        Assert.True(lines.RequestToSend);
    }

    [Fact]
    public async Task ThrowsWhenADeviceByteIsNotAcknowledgedAsync()
    {
        var lines = new FakeModemControlLines([false, false, true]);
        using var bus = CreateBus(lines);
        bus.Open();

        await Assert.ThrowsAsync<IOException>(() => bus.WriteAsync(0x20, ReadOnlyMemory<byte>.Empty, CancellationToken.None));

        Assert.True(lines.DataTerminalReady);
        Assert.True(lines.RequestToSend);
    }

    [Fact]
    public async Task SupportsInvertedControlAndStatusLinePolarityAsync()
    {
        var lines = new FakeModemControlLines([true, true, true, true]);
        using var bus = new SerialControlLineMrds192Bus(
            lines,
            new Mrds192BusTiming(TimeSpan.Zero, TimeSpan.Zero),
            dtrEnabledIsSdaHigh: false,
            rtsEnabledIsSclHigh: false,
            ctsHighIsSdaHigh: false);
        bus.Open();

        await bus.WriteAsync(0x20, ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.False(lines.DataTerminalReady);
        Assert.False(lines.RequestToSend);
    }

    [Fact]
    public async Task PerformsRandomReadWithRepeatedStartAsync()
    {
        var lines = new FakeModemControlLines([false, false, false, false, false, .. GetBits(0xa5), .. GetBits(0x3c)]);
        using var bus = CreateBus(lines);
        bus.Open();

        var result = await bus.ReadAsync(0x70, 2, CancellationToken.None);

        Assert.Equal([0xa5, 0x3c], result);
        Assert.True(ContainsBitSequence(lines.ClockRisingDataValues, GetBits(0xd6)));
        Assert.True(ContainsBitSequence(lines.ClockRisingDataValues, GetBits(0x70)));
        Assert.True(ContainsBitSequence(lines.ClockRisingDataValues, GetBits(0xd7)));
    }

    [Fact]
    public async Task HonorsCancellationBeforeStartingATransactionAsync()
    {
        var lines = new FakeModemControlLines();
        using var bus = CreateBus(lines);
        bus.Open();
        var clockRisesBeforeWrite = lines.ClockRisingDataValues.Count;
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => bus.WriteAsync(0x20, new byte[] { 0x01 }, cancellationTokenSource.Token));

        Assert.Equal(clockRisesBeforeWrite, lines.ClockRisingDataValues.Count);
    }

    [Fact]
    public void ClosesAndDisposesTheOwnedModemLines()
    {
        var lines = new FakeModemControlLines();
        var bus = CreateBus(lines);
        bus.Open();

        bus.Close();
        bus.Dispose();

        Assert.False(lines.IsOpen);
        Assert.True(lines.IsDisposed);
    }

    private static SerialControlLineMrds192Bus CreateBus(FakeModemControlLines lines) =>
        new(lines, new Mrds192BusTiming(TimeSpan.Zero, TimeSpan.Zero));

    private static bool[] GetBits(byte value) =>
        Enumerable.Range(0, 8).Select(index => (value & (1 << (7 - index))) != 0).ToArray();

    private static bool ContainsBitSequence(IReadOnlyList<bool> values, IReadOnlyList<bool> sequence)
    {
        for (var start = 0; start <= values.Count - sequence.Count; start++)
        {
            if (Enumerable.Range(start, sequence.Count).All(index => values[index] == sequence[index - start]))
            {
                return true;
            }
        }

        return false;
    }
}
