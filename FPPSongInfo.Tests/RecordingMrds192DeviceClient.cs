using FPPSongInfo.Rds;

namespace FPPSongInfo.Tests;

internal sealed class RecordingMrds192DeviceClient : IMrds192DeviceClient
{
    private readonly List<string> _radioTexts = [];

    public int InitializationCount { get; private set; }

    public IReadOnlyList<string> RadioTexts
    {
        get
        {
            lock (_radioTexts)
            {
                return _radioTexts.ToArray();
            }
        }
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        InitializationCount++;
        return Task.CompletedTask;
    }

    public Task WriteRadioTextAsync(string radioText, CancellationToken cancellationToken)
    {
        lock (_radioTexts)
        {
            _radioTexts.Add(radioText);
        }

        return Task.CompletedTask;
    }
}
