using System.Text;
using FPPSongInfo.Configuration;
using FPPSongInfo.Rds;

namespace FPPSongInfo.Tests;

public sealed class Mrds192DeviceClientTests
{
    [Fact]
    public async Task InitializesOnlyVolatileOwnedRegistersInRequiredOrderAsync()
    {
        var bus = new RecordingMrds192Bus([[0], [0x02]]);
        var client = CreateClient(bus);

        await client.InitializeAsync(CancellationToken.None);

        Assert.True(bus.IsOpen);
        Assert.Equal(
            [0x02, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x76, 0x1f],
            bus.Writes.Select(write => write.RegisterAddress));
        Assert.Equal("LTSHOW  ", Encoding.ASCII.GetString(bus.Writes[0].Data));
        Assert.Equal(new byte[] { 2 }, bus.Writes[1].Data);
        Assert.Equal(new byte[] { 3 }, bus.Writes[2].Data);
        Assert.Equal(new byte[] { 5 }, bus.Writes[3].Data);
        Assert.Equal(new byte[] { 0 }, bus.Writes[4].Data);
        Assert.Equal(new byte[] { 0 }, bus.Writes[5].Data);
        Assert.Equal("Compound Radio", Encoding.ASCII.GetString(bus.Writes[6].Data));
        Assert.Equal(new byte[] { 14 }, bus.Writes[7].Data);
        Assert.Equal(new byte[] { 0x03 }, bus.Writes[8].Data);
        Assert.DoesNotContain(bus.Writes, write => write.RegisterAddress == 0x45);
    }

    [Fact]
    public void UsesTheRequiredBufferedProgramServiceDelay()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(620), Mrds192DeviceClient.BufferedProgramServiceWriteDelay);
    }

    [Fact]
    public async Task DisablesRadioTextUntilTheNewBufferAndTypeAreWrittenAsync()
    {
        var bus = new RecordingMrds192Bus([[0], [0x02]]);
        var client = CreateClient(bus);
        await client.InitializeAsync(CancellationToken.None);
        bus.FailNextWriteForRegister = 0x1f;

        await Assert.ThrowsAsync<IOException>(() => client.WriteRadioTextAsync("Artist - Title", CancellationToken.None));
        await client.WriteRadioTextAsync("Artist - Title", CancellationToken.None);

        Assert.Equal(0x1f, bus.Writes[^3].RegisterAddress);
        Assert.Equal(new byte[] { 0 }, bus.Writes[^3].Data);
        Assert.Equal(0x20, bus.Writes[^2].RegisterAddress);
        Assert.Equal(0x1f, bus.Writes[^1].RegisterAddress);
        Assert.Equal(new byte[] { 0x01 }, bus.Writes[^1].Data);
    }

    private static Mrds192DeviceClient CreateClient(RecordingMrds192Bus bus) =>
        new(
            bus,
            new RdsOptions
            {
                PortName = "COM3",
                StaticProgramService = "LTSHOW",
                DynamicProgramService = "Compound Radio",
                DynamicPsMode = DynamicPsMode.SpaceSeparatedScrolling,
                LabelPeriod = TimeSpan.FromSeconds(2.5),
                LoopDelay = TimeSpan.FromSeconds(5.4)
            },
            TimeSpan.Zero);
}
