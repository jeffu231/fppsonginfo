using FPPSongInfo.Rds;

namespace FPPSongInfo.Tests;

internal sealed class FakeModemControlLines(IEnumerable<bool>? clearToSendValues = null) : IModemControlLines
{
    private readonly Queue<bool> _clearToSendValues = new(clearToSendValues ?? []);
    private bool _dataTerminalReady;
    private bool _requestToSend;

    public List<bool> ClockRisingDataValues { get; } = [];

    public bool ClearToSend => _clearToSendValues.TryDequeue(out var value) && value;

    public bool DataTerminalReady
    {
        get => _dataTerminalReady;
        set => _dataTerminalReady = value;
    }

    public bool IsDisposed { get; private set; }

    public bool IsOpen { get; private set; }

    public bool RequestToSend
    {
        get => _requestToSend;
        set
        {
            if (!_requestToSend && value)
            {
                ClockRisingDataValues.Add(_dataTerminalReady);
            }

            _requestToSend = value;
        }
    }

    public void Close() => IsOpen = false;

    public void Dispose()
    {
        IsDisposed = true;
        IsOpen = false;
    }

    public void Open() => IsOpen = true;
}
