using Microsoft.Win32.SafeHandles;
using System.Text;
using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;
using Windows.Win32.Devices.Usb;

namespace UsbInspector.Core.Services;

/// <summary>Result of querying a single hub port connection.</summary>
public sealed class NodeConnection
{
    public uint Port { get; init; }
    public byte SpeedByte { get; init; }
    public bool IsHub { get; init; }
    public ushort DeviceAddress { get; init; }
    public UsbPortConnectionStatus ConnectionStatus { get; init; }
    public byte CurrentConfigurationValue { get; init; }
    public UsbDeviceDescriptorInfo? DeviceDescriptor { get; init; }
}

/// <summary>Low-level IOCTL queries issued against an open USB hub handle.</summary>
public static unsafe class HubQueries
{
    public static ushort? GetHighestPortNumber(SafeFileHandle hub)
    {
        int size = sizeof(USB_HUB_INFORMATION_EX);
        var buffer = new byte[size];
        if (!NativeDevice.Control(hub, UsbIoctl.IOCTL_USB_GET_HUB_INFORMATION_EX, buffer, out _))
        {
            return null;
        }

        fixed (byte* p = buffer)
        {
            var info = (USB_HUB_INFORMATION_EX*)p;
            return info->HighestPortNumber;
        }
    }

    public static NodeConnection? GetNodeConnection(SafeFileHandle hub, uint port)
    {
        int size = sizeof(USB_NODE_CONNECTION_INFORMATION_EX) + (32 * sizeof(USB_PIPE_INFO));
        var buffer = new byte[size];

        // Set ConnectionIndex (first field).
        BitConverter.TryWriteBytes(buffer.AsSpan(0, 4), port);

        if (!NativeDevice.Control(hub, UsbIoctl.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX, buffer, out _))
        {
            return null;
        }

        fixed (byte* p = buffer)
        {
            var info = (USB_NODE_CONNECTION_INFORMATION_EX*)p;
            USB_DEVICE_DESCRIPTOR d = info->DeviceDescriptor;
            var status = (UsbPortConnectionStatus)(int)info->ConnectionStatus;

            UsbDeviceDescriptorInfo? deviceDescriptor = null;
            if (status == UsbPortConnectionStatus.DeviceConnected)
            {
                deviceDescriptor = new UsbDeviceDescriptorInfo
                {
                    BcdUsb = d.bcdUSB,
                    DeviceClass = d.bDeviceClass,
                    DeviceSubClass = d.bDeviceSubClass,
                    DeviceProtocol = d.bDeviceProtocol,
                    MaxPacketSize0 = d.bMaxPacketSize0,
                    IdVendor = d.idVendor,
                    IdProduct = d.idProduct,
                    BcdDevice = d.bcdDevice,
                    ManufacturerIndex = d.iManufacturer,
                    ProductIndex = d.iProduct,
                    SerialNumberIndex = d.iSerialNumber,
                    NumConfigurations = d.bNumConfigurations,
                };
            }

            return new NodeConnection
            {
                Port = port,
                SpeedByte = info->Speed,
                IsHub = info->DeviceIsHub != 0,
                DeviceAddress = info->DeviceAddress,
                ConnectionStatus = status,
                CurrentConfigurationValue = info->CurrentConfigurationValue,
                DeviceDescriptor = deviceDescriptor,
            };
        }
    }

    /// <summary>Reads USB 3.x lane/gen operating flags for a port, if available.</summary>
    public static USB_NODE_CONNECTION_INFORMATION_EX_V2_FLAGS? GetNodeConnectionV2Flags(SafeFileHandle hub, uint port)
    {
        int size = sizeof(USB_NODE_CONNECTION_INFORMATION_EX_V2);
        var buffer = new byte[size];
        BitConverter.TryWriteBytes(buffer.AsSpan(0, 4), port);            // ConnectionIndex
        BitConverter.TryWriteBytes(buffer.AsSpan(4, 4), (uint)size);      // Length

        if (!NativeDevice.Control(hub, UsbIoctl.IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX_V2, buffer, out _))
        {
            return null;
        }

        fixed (byte* p = buffer)
        {
            var info = (USB_NODE_CONNECTION_INFORMATION_EX_V2*)p;
            return info->Flags;
        }
    }

    public static string? GetExternalHubName(SafeFileHandle hub, uint port)
        => ReadConnectionString(hub, port, UsbIoctl.IOCTL_USB_GET_NODE_CONNECTION_NAME);

    public static string? GetDriverKeyName(SafeFileHandle hub, uint port)
        => ReadConnectionString(hub, port, UsbIoctl.IOCTL_USB_GET_NODE_CONNECTION_DRIVERKEY_NAME);

    /// <summary>Reads a host controller's root hub name (USB_ROOT_HUB_NAME: name at offset 4).</summary>
    public static string? GetRootHubName(SafeFileHandle controller)
    {
        const int stringOffset = 4;
        var buffer = new byte[2048];
        if (!NativeDevice.Control(controller, UsbIoctl.IOCTL_USB_GET_ROOT_HUB_NAME, buffer, out uint returned)
            || returned <= stringOffset)
        {
            return null;
        }

        uint actualLength = BitConverter.ToUInt32(buffer, 0);
        int stringBytes = (int)Math.Min(actualLength, returned) - stringOffset;
        if (stringBytes <= 0)
        {
            return null;
        }

        return Encoding.Unicode.GetString(buffer, stringOffset, stringBytes).TrimEnd('\0');
    }

    // Both USB_NODE_CONNECTION_NAME and USB_NODE_CONNECTION_DRIVERKEY_NAME share the layout:
    // uint ConnectionIndex; uint ActualLength; WCHAR Name[1]; -> string starts at byte offset 8.
    private static string? ReadConnectionString(SafeFileHandle hub, uint port, uint ioctl)
    {
        const int stringOffset = 8;
        var buffer = new byte[2048];
        BitConverter.TryWriteBytes(buffer.AsSpan(0, 4), port);

        if (!NativeDevice.Control(hub, ioctl, buffer, out uint returned) || returned <= stringOffset)
        {
            return null;
        }

        uint actualLength = BitConverter.ToUInt32(buffer, 4);
        int stringBytes = (int)Math.Min(actualLength, returned) - stringOffset;
        if (stringBytes <= 0)
        {
            return null;
        }

        return Encoding.Unicode.GetString(buffer, stringOffset, stringBytes).TrimEnd('\0');
    }
}
