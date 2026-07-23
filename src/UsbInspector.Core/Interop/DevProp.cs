using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.Properties;

namespace UsbInspector.Core.Interop;

/// <summary>Reads Unified Device Property Model (DEVPKEY) values from a devnode via CfgMgr32.</summary>
public static class DevProp
{
    // DEVPROPTYPE base type + modifier constants (devpropdef.h).
    private const uint DEVPROP_TYPEMOD_LIST = 0x00002000;
    private const uint DEVPROP_TYPE_SBYTE = 0x02;
    private const uint DEVPROP_TYPE_BYTE = 0x03;
    private const uint DEVPROP_TYPE_INT16 = 0x04;
    private const uint DEVPROP_TYPE_UINT16 = 0x05;
    private const uint DEVPROP_TYPE_INT32 = 0x06;
    private const uint DEVPROP_TYPE_UINT32 = 0x07;
    private const uint DEVPROP_TYPE_INT64 = 0x08;
    private const uint DEVPROP_TYPE_UINT64 = 0x09;
    private const uint DEVPROP_TYPE_GUID = 0x0D;
    private const uint DEVPROP_TYPE_FILETIME = 0x10;
    private const uint DEVPROP_TYPE_BOOLEAN = 0x11;
    private const uint DEVPROP_TYPE_STRING = 0x12;
    private const uint DEVPROP_TYPE_MASK = 0x00000FFF;

    public static unsafe bool TryGet(uint devInst, in DEVPROPKEY key, out DEVPROPTYPE type, out byte[] data)
    {
        type = default;
        data = Array.Empty<byte>();

        uint size = 0;
        CONFIGRET cr = PInvoke.CM_Get_DevNode_Property(devInst, in key, out type, null, ref size, 0);
        if (size == 0)
        {
            return false;
        }

        var buffer = new byte[size];
        fixed (byte* p = buffer)
        {
            cr = PInvoke.CM_Get_DevNode_Property(devInst, in key, out type, p, ref size, 0);
        }

        if (cr != CONFIGRET.CR_SUCCESS)
        {
            return false;
        }

        data = buffer;
        return true;
    }

    public static string? GetString(uint devInst, in DEVPROPKEY key)
        => TryGet(devInst, in key, out _, out byte[] data) ? DecodeString(data) : null;

    public static string[] GetStringList(uint devInst, in DEVPROPKEY key)
        => TryGet(devInst, in key, out _, out byte[] data) ? DecodeStringList(data) : Array.Empty<string>();

    public static uint? GetUInt32(uint devInst, in DEVPROPKEY key)
        => TryGet(devInst, in key, out _, out byte[] data) && data.Length >= 4
            ? BitConverter.ToUInt32(data, 0)
            : null;

    public static bool? GetBoolean(uint devInst, in DEVPROPKEY key)
        => TryGet(devInst, in key, out _, out byte[] data) && data.Length >= 1
            ? data[0] != 0
            : null;

    /// <summary>Formats any property value into a human-readable string for the raw view.</summary>
    public static string Format(DEVPROPTYPE type, byte[] data)
    {
        uint baseType = (uint)type & DEVPROP_TYPE_MASK;
        bool isList = ((uint)type & DEVPROP_TYPEMOD_LIST) != 0;

        try
        {
            if (baseType == DEVPROP_TYPE_STRING)
            {
                return isList ? string.Join("; ", DecodeStringList(data)) : DecodeString(data);
            }

            return baseType switch
            {
                DEVPROP_TYPE_BOOLEAN => data.Length >= 1 && data[0] != 0 ? "True" : "False",
                DEVPROP_TYPE_BYTE or DEVPROP_TYPE_SBYTE => data.Length >= 1 ? data[0].ToString() : string.Empty,
                DEVPROP_TYPE_UINT16 or DEVPROP_TYPE_INT16 => data.Length >= 2 ? BitConverter.ToUInt16(data, 0).ToString() : string.Empty,
                DEVPROP_TYPE_UINT32 or DEVPROP_TYPE_INT32 => data.Length >= 4 ? BitConverter.ToUInt32(data, 0).ToString() : string.Empty,
                DEVPROP_TYPE_UINT64 or DEVPROP_TYPE_INT64 => data.Length >= 8 ? BitConverter.ToUInt64(data, 0).ToString() : string.Empty,
                DEVPROP_TYPE_GUID => data.Length >= 16 ? new Guid(data.AsSpan(0, 16)).ToString("B") : string.Empty,
                DEVPROP_TYPE_FILETIME => DecodeFileTime(data),
                _ => "0x" + Convert.ToHexString(data),
            };
        }
        catch
        {
            return "0x" + Convert.ToHexString(data);
        }
    }

    private static string DecodeString(byte[] data)
        => Encoding.Unicode.GetString(data).TrimEnd('\0');

    private static string[] DecodeStringList(byte[] data)
        => Encoding.Unicode.GetString(data)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);

    private static string DecodeFileTime(byte[] data)
    {
        if (data.Length < 8)
        {
            return string.Empty;
        }

        long ft = BitConverter.ToInt64(data, 0);
        try
        {
            return DateTimeOffset.FromFileTime(ft).ToString("yyyy-MM-dd");
        }
        catch
        {
            return string.Empty;
        }
    }
}
