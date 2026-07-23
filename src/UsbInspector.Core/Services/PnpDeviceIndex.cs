using UsbInspector.Core.Interop;
using Windows.Win32;

namespace UsbInspector.Core.Services;

/// <summary>
/// Builds a lookup from a USB device's driver key (as returned by
/// IOCTL_USB_GET_NODE_CONNECTION_DRIVERKEY_NAME) to its PnP devnode, so that per-device
/// driver/PnP properties can be attached to nodes discovered during the hub walk.
/// </summary>
public sealed class PnpDeviceIndex
{
    private readonly Dictionary<string, uint> _byDriverKey = new(StringComparer.OrdinalIgnoreCase);

    public static PnpDeviceIndex Build()
    {
        var index = new PnpDeviceIndex();
        index.Add(PInvoke.GUID_DEVINTERFACE_USB_DEVICE);
        index.Add(PInvoke.GUID_DEVINTERFACE_USB_HUB);
        return index;
    }

    private void Add(Guid interfaceGuid)
    {
        foreach (DeviceInterfaceInstance instance in DeviceInterfaceEnumerator.Enumerate(interfaceGuid))
        {
            string? driverKey = DevProp.GetString(instance.DevInst, PInvoke.DEVPKEY_Device_Driver);
            if (!string.IsNullOrEmpty(driverKey))
            {
                _byDriverKey[driverKey] = instance.DevInst;
            }
        }
    }

    public uint? Resolve(string? driverKey)
        => driverKey is not null && _byDriverKey.TryGetValue(driverKey, out uint devInst) ? devInst : null;
}
