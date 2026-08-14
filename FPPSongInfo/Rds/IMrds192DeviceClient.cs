namespace FPPSongInfo.Rds;

internal interface IMrds192DeviceClient
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task WriteRadioTextAsync(string radioText, CancellationToken cancellationToken);
}
