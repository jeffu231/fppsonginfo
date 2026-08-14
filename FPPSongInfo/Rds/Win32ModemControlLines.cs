using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FPPSongInfo.Rds;

internal sealed class Win32ModemControlLines(string portName) : IModemControlLines
{
    private const uint ClearDtr = 6;
    private const uint ClearRts = 4;
    private const uint CreateFileOpenExisting = 3;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint ModemStatusCtsOn = 0x10;
    private const uint SetDtr = 5;
    private const uint SetRts = 3;
    private SafeFileHandle? _portHandle;

    public bool ClearToSend
    {
        get
        {
            var portHandle = GetOpenPortHandle();
            if (!GetCommModemStatus(portHandle, out var modemStatus))
            {
                throw CreateIOException("read the CTS modem status");
            }

            return (modemStatus & ModemStatusCtsOn) != 0;
        }
    }

    public bool DataTerminalReady
    {
        get => throw new NotSupportedException("The Win32 modem-control adapter does not query DTR state.");
        set => SetControlLine(value ? SetDtr : ClearDtr, "set DTR");
    }

    public bool IsOpen => _portHandle is { IsClosed: false, IsInvalid: false };

    public bool RequestToSend
    {
        get => throw new NotSupportedException("The Win32 modem-control adapter does not query RTS state.");
        set => SetControlLine(value ? SetRts : ClearRts, "set RTS");
    }

    public void Close()
    {
        _portHandle?.Dispose();
        _portHandle = null;
    }

    public void Dispose() => Close();

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        var portHandle = CreateFile(
            $@"\\.\{portName}",
            GenericRead | GenericWrite,
            0,
            IntPtr.Zero,
            CreateFileOpenExisting,
            0,
            IntPtr.Zero);
        if (portHandle.IsInvalid)
        {
            portHandle.Dispose();
            throw CreateIOException($"open serial port {portName}");
        }

        _portHandle = portHandle;
    }

    private static IOException CreateIOException(string operation) =>
        new($"Could not {operation} for the MRDS192 COM-line transport.", new Win32Exception(Marshal.GetLastWin32Error()));

    private SafeFileHandle GetOpenPortHandle() =>
        IsOpen ? _portHandle! : throw new InvalidOperationException("The MRDS192 COM-line transport is not open.");

    private void SetControlLine(uint function, string operation)
    {
        if (!EscapeCommFunction(GetOpenPortHandle(), function))
        {
            throw CreateIOException(operation);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EscapeCommFunction(SafeFileHandle handle, uint function);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCommModemStatus(SafeFileHandle handle, out uint modemStatus);
}
