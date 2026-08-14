using FPPSongInfo.Rds;

namespace FPPSongInfo.Tests;

internal sealed class RecordingMrds192Bus : IMrds192Bus
{
    private readonly Queue<byte[]> _readResults;

    public RecordingMrds192Bus(IEnumerable<byte[]> readResults)
    {
        _readResults = new Queue<byte[]>(readResults);
    }

    public bool IsOpen { get; private set; }

    public byte? FailNextWriteForRegister { get; set; }

    public List<(byte RegisterAddress, byte[] Data)> Writes { get; } = [];

    public void Close() => IsOpen = false;

    public void Dispose()
    {
    }

    public void Open() => IsOpen = true;

    public Task<byte[]> ReadAsync(byte registerAddress, int length, CancellationToken cancellationToken) =>
        Task.FromResult(_readResults.Dequeue());

    public Task WriteAsync(byte registerAddress, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        if (FailNextWriteForRegister is { } registerToFail && registerToFail == registerAddress)
        {
            FailNextWriteForRegister = null;
            return Task.FromException(new IOException("Simulated bus write failure."));
        }

        Writes.Add((registerAddress, data.ToArray()));
        return Task.CompletedTask;
    }
}
