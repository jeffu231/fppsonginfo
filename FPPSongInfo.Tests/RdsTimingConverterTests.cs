using FPPSongInfo.Rds;

namespace FPPSongInfo.Tests;

public sealed class RdsTimingConverterTests
{
    [Fact]
    public void ConvertsConfiguredTimingToExpectedRawValues()
    {
        var labelRaw = RdsTimingConverter.ToLabelPeriodRaw(TimeSpan.FromSeconds(2.5));
        var loopDelayRaw = RdsTimingConverter.ToLoopDelayRaw(TimeSpan.FromSeconds(5.4));

        Assert.Equal((byte)5, labelRaw);
        Assert.Equal((byte)2, loopDelayRaw);
        Assert.Equal(TimeSpan.FromSeconds(2.7), RdsTimingConverter.GetEffectiveLabelPeriod(labelRaw));
        Assert.Equal(TimeSpan.FromSeconds(5.4), RdsTimingConverter.GetEffectiveLoopDelay(loopDelayRaw));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1000, 255)]
    public void ClampsLabelPeriodToRegisterRange(double seconds, byte expected)
    {
        var rawValue = RdsTimingConverter.ToLabelPeriodRaw(TimeSpan.FromSeconds(seconds));

        Assert.Equal(expected, rawValue);
    }
}
