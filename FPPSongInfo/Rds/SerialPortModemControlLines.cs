using System.IO.Ports;

namespace FPPSongInfo.Rds;

internal sealed class SerialPortModemControlLines(SerialPort serialPort) : IModemControlLines
{
    private readonly SerialPort _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));

    public bool ClearToSend => _serialPort.CtsHolding;

    public bool DataTerminalReady
    {
        get => _serialPort.DtrEnable;
        set => _serialPort.DtrEnable = value;
    }

    public bool IsOpen => _serialPort.IsOpen;

    public bool RequestToSend
    {
        get => _serialPort.RtsEnable;
        set => _serialPort.RtsEnable = value;
    }

    public void Close() => _serialPort.Close();

    public void Dispose() => _serialPort.Dispose();

    public void Open() => _serialPort.Open();
}
