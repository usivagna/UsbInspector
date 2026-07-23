using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

namespace UsbInspector.Core.Interop;

/// <summary>A single device-interface instance discovered via SetupAPI.</summary>
public readonly record struct DeviceInterfaceInstance(string DevicePath, uint DevInst, string InstanceId);

/// <summary>
/// Enumerates present device interfaces for a given interface class GUID using SetupAPI.
/// </summary>
public static class DeviceInterfaceEnumerator
{
    public static unsafe IReadOnlyList<DeviceInterfaceInstance> Enumerate(Guid interfaceGuid)
    {
        var results = new List<DeviceInterfaceInstance>();

        using SetupDiDestroyDeviceInfoListSafeHandle deviceInfoSet = PInvoke.SetupDiGetClassDevs(
            interfaceGuid,
            null,
            HWND.Null,
            SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_PRESENT | SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_DEVICEINTERFACE);

        if (deviceInfoSet.IsInvalid)
        {
            return results;
        }

        // cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W: the DevicePath field lives at byte
        // offset 4, but cbSize must be the padded struct size (8 on 64-bit, 6 on 32-bit).
        uint detailCbSize = (uint)(IntPtr.Size == 8 ? 8 : 6);
        const int devicePathByteOffset = 4;

        for (uint index = 0; ; index++)
        {
            var interfaceData = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)sizeof(SP_DEVICE_INTERFACE_DATA) };
            if (!PInvoke.SetupDiEnumDeviceInterfaces(deviceInfoSet, null, interfaceGuid, index, ref interfaceData))
            {
                break; // ERROR_NO_MORE_ITEMS
            }

            uint requiredSize = 0;
            PInvoke.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, in interfaceData, null, 0, &requiredSize, null);
            if (requiredSize == 0)
            {
                continue;
            }

            byte[] buffer = new byte[requiredSize];
            fixed (byte* pBuffer = buffer)
            {
                var pDetail = (SP_DEVICE_INTERFACE_DETAIL_DATA_W*)pBuffer;
                pDetail->cbSize = detailCbSize;

                var devInfoData = new SP_DEVINFO_DATA { cbSize = (uint)sizeof(SP_DEVINFO_DATA) };
                if (!PInvoke.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet, in interfaceData, pDetail, requiredSize, null, &devInfoData))
                {
                    continue;
                }

                string devicePath = Marshal.PtrToStringUni((IntPtr)(pBuffer + devicePathByteOffset)) ?? string.Empty;
                string instanceId = CmDevice.GetDeviceId(devInfoData.DevInst);
                results.Add(new DeviceInterfaceInstance(devicePath, devInfoData.DevInst, instanceId));
            }
        }

        return results;
    }
}
