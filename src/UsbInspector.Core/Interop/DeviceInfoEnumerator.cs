using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

namespace UsbInspector.Core.Interop;

/// <summary>A single present devnode discovered via SetupAPI (all classes).</summary>
public readonly record struct DeviceInfoInstance(uint DevInst, string InstanceId);

/// <summary>
/// Enumerates all present devnodes on the system using SetupAPI. Used to locate device kinds
/// (such as USB4 host routers) that Windows does not expose through a device-interface class GUID.
/// </summary>
public static class DeviceInfoEnumerator
{
    public static unsafe IReadOnlyList<DeviceInfoInstance> EnumerateAllPresent()
    {
        var results = new List<DeviceInfoInstance>();

        using SetupDiDestroyDeviceInfoListSafeHandle deviceInfoSet = PInvoke.SetupDiGetClassDevs(
            null,
            null,
            HWND.Null,
            SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_PRESENT | SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_ALLCLASSES);

        if (deviceInfoSet.IsInvalid)
        {
            return results;
        }

        for (uint index = 0; ; index++)
        {
            var info = new SP_DEVINFO_DATA { cbSize = (uint)sizeof(SP_DEVINFO_DATA) };
            if (!PInvoke.SetupDiEnumDeviceInfo(deviceInfoSet, index, ref info))
            {
                break; // ERROR_NO_MORE_ITEMS
            }

            string instanceId = CmDevice.GetDeviceId(info.DevInst);
            results.Add(new DeviceInfoInstance(info.DevInst, instanceId));
        }

        return results;
    }
}
