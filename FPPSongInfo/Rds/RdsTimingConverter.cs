namespace FPPSongInfo.Rds;

internal static class RdsTimingConverter
{
    private const double LabelPeriodSecondsPerRawValue = 0.54;
    private const double LoopDelaySecondsPerRawValue = 2.7;

    internal static TimeSpan GetEffectiveLabelPeriod(byte rawValue) =>
        TimeSpan.FromSeconds(rawValue * LabelPeriodSecondsPerRawValue);

    internal static TimeSpan GetEffectiveLoopDelay(byte rawValue) =>
        TimeSpan.FromSeconds(rawValue * LoopDelaySecondsPerRawValue);

    internal static byte ToLabelPeriodRaw(TimeSpan labelPeriod) =>
        ToRawValue(labelPeriod, LabelPeriodSecondsPerRawValue);

    internal static byte ToLoopDelayRaw(TimeSpan loopDelay) =>
        ToRawValue(loopDelay, LoopDelaySecondsPerRawValue);

    private static byte ToRawValue(TimeSpan period, double secondsPerRawValue)
    {
        var rounded = Math.Round(period.TotalSeconds / secondsPerRawValue, MidpointRounding.AwayFromZero);
        return (byte)Math.Clamp(rounded, 0, byte.MaxValue);
    }
}
