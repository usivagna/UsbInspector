using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

namespace UsbInspector.Core.Interop;

/// <summary>Helpers for opening USB device objects and issuing buffered IOCTLs.</summary>
public static class NativeDevice
{
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint GENERIC_READ = 0x80000000;

    /// <summary>Opens a device object by its interface path (e.g. a hub or host controller).</summary>
    public static SafeFileHandle Open(string devicePath, bool writeAccess = true)
    {
        uint access = writeAccess ? GENERIC_WRITE : 0;
        return PInvoke.CreateFile(
            devicePath,
            access,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            0,
            null);
    }

    /// <summary>
    /// Issues a buffered IOCTL where the same buffer is used for input and output
    /// (the common pattern for USB node/descriptor queries).
    /// </summary>
    public static unsafe bool Control(SafeFileHandle handle, uint ioControlCode, byte[] buffer, out uint bytesReturned)
    {
        bytesReturned = 0;
        fixed (byte* p = buffer)
        {
            uint returned = 0;
            bool ok = PInvoke.DeviceIoControl(
                handle,
                ioControlCode,
                p,
                (uint)buffer.Length,
                p,
                (uint)buffer.Length,
                &returned,
                null);
            bytesReturned = returned;
            return ok;
        }
    }

    /// <summary>Issues a buffered IOCTL with distinct input and output buffers.</summary>
    public static unsafe bool Control(
        SafeFileHandle handle,
        uint ioControlCode,
        byte[] input,
        byte[] output,
        out uint bytesReturned)
    {
        bytesReturned = 0;
        fixed (byte* pin = input)
        fixed (byte* pout = output)
        {
            uint returned = 0;
            bool ok = PInvoke.DeviceIoControl(
                handle,
                ioControlCode,
                pin,
                (uint)input.Length,
                pout,
                (uint)output.Length,
                &returned,
                null);
            bytesReturned = returned;
            return ok;
        }
    }
}
