namespace FPPSongInfo.Rds;

internal readonly record struct Mrds192BusTiming(TimeSpan ClockTransitionDelay, TimeSpan BusFreeDelay)
{
    internal static Mrds192BusTiming Create(bool slow) => slow
        ? new(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(5))
        : new(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1));
}
