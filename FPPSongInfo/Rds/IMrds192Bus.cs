namespace FPPSongInfo.Rds;

internal interface IMrds192Bus : IDisposable
{
    void Close();

    void Open();

    Task<byte[]> ReadAsync(byte registerAddress, int length, CancellationToken cancellationToken);

    Task WriteAsync(byte registerAddress, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}
