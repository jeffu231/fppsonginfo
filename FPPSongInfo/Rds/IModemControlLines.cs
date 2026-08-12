namespace FPPSongInfo.Rds;

internal interface IModemControlLines : IDisposable
{
    bool ClearToSend { get; }

    bool DataTerminalReady { get; set; }

    bool IsOpen { get; }

    bool RequestToSend { get; set; }

    void Close();

    void Open();
}
