namespace FPPSongInfo.Tests;

internal static class AsyncAssert
{
    public static async Task EventuallyAsync(Func<bool> condition)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, cancellationTokenSource.Token);
        }
    }
}
