using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;

namespace UsbInspector.Core.Services;

/// <summary>Parses raw USB descriptor byte buffers into structured models.</summary>
public static class DescriptorParser
{
    public static UsbDeviceDescriptorInfo? ParseDevice(ReadOnlySpan<byte> data)
    {
        if (data.Length < 18)
        {
            return null;
        }

        return new UsbDeviceDescriptorInfo
        {
            BcdUsb = (ushort)(data[2] | (data[3] << 8)),
            DeviceClass = data[4],
            DeviceSubClass = data[5],
            DeviceProtocol = data[6],
            MaxPacketSize0 = data[7],
            IdVendor = (ushort)(data[8] | (data[9] << 8)),
            IdProduct = (ushort)(data[10] | (data[11] << 8)),
            BcdDevice = (ushort)(data[12] | (data[13] << 8)),
            ManufacturerIndex = data[14],
            ProductIndex = data[15],
            SerialNumberIndex = data[16],
            NumConfigurations = data[17],
        };
    }

    /// <summary>
    /// Parses a full configuration descriptor blob (the 9-byte config header followed by all
    /// interface, endpoint and class-specific descriptors up to wTotalLength).
    /// </summary>
    public static UsbConfigurationDescriptorInfo? ParseConfiguration(ReadOnlySpan<byte> data)
    {
        if (data.Length < 9 || data[1] != UsbIoctl.USB_CONFIGURATION_DESCRIPTOR_TYPE)
        {
            return null;
        }

        int totalLength = data[2] | (data[3] << 8);
        totalLength = Math.Min(totalLength, data.Length);

        var config = new UsbConfigurationDescriptorInfo
        {
            NumInterfaces = data[4],
            ConfigurationValue = data[5],
            ConfigurationStringIndex = data[6],
            Attributes = data[7],
            MaxPowerRaw = data[8],
        };

        UsbInterfaceDescriptorInfo? currentInterface = null;
        int offset = data[0]; // skip config header (bLength)

        while (offset + 2 <= totalLength)
        {
            int bLength = data[offset];
            int bType = data[offset + 1];
            if (bLength < 2 || offset + bLength > totalLength)
            {
                break;
            }

            ReadOnlySpan<byte> desc = data.Slice(offset, bLength);

            switch (bType)
            {
                case UsbIoctl.USB_INTERFACE_DESCRIPTOR_TYPE when bLength >= 9:
                    currentInterface = new UsbInterfaceDescriptorInfo
                    {
                        InterfaceNumber = desc[2],
                        AlternateSetting = desc[3],
                        NumEndpoints = desc[4],
                        InterfaceClass = desc[5],
                        InterfaceSubClass = desc[6],
                        InterfaceProtocol = desc[7],
                        InterfaceStringIndex = desc[8],
                        ClassName = UsbClassNames.ForClass(desc[5]),
                    };
                    config.Interfaces.Add(currentInterface);
                    break;

                case UsbIoctl.USB_ENDPOINT_DESCRIPTOR_TYPE when bLength >= 7 && currentInterface is not null:
                    currentInterface.Endpoints.Add(new UsbEndpointDescriptorInfo
                    {
                        EndpointAddress = desc[2],
                        Attributes = desc[3],
                        MaxPacketSize = (ushort)(desc[4] | (desc[5] << 8)),
                        Interval = desc[6],
                    });
                    break;
            }

            offset += bLength;
        }

        return config;
    }

    /// <summary>Parses a BOS descriptor blob into its device capability descriptors.</summary>
    public static List<UsbBosCapabilityInfo> ParseBos(ReadOnlySpan<byte> data)
    {
        var result = new List<UsbBosCapabilityInfo>();
        if (data.Length < 5 || data[1] != UsbIoctl.USB_BOS_DESCRIPTOR_TYPE)
        {
            return result;
        }

        int totalLength = data[2] | (data[3] << 8);
        totalLength = Math.Min(totalLength, data.Length);
        int offset = data[0];

        while (offset + 3 <= totalLength)
        {
            int bLength = data[offset];
            if (bLength < 3 || offset + bLength > totalLength)
            {
                break;
            }

            byte capabilityType = data[offset + 2];
            byte[] raw = data.Slice(offset, bLength).ToArray();
            result.Add(new UsbBosCapabilityInfo
            {
                CapabilityType = capabilityType,
                CapabilityName = CapabilityName(capabilityType),
                RawData = raw,
                Summary = SummarizeCapability(capabilityType, raw),
            });

            offset += bLength;
        }

        return result;
    }

    private static string CapabilityName(byte type) => type switch
    {
        UsbIoctl.USB_DEVICE_CAPABILITY_WIRELESS_USB => "Wireless USB",
        UsbIoctl.USB_DEVICE_CAPABILITY_USB20_EXTENSION => "USB 2.0 Extension (LPM)",
        UsbIoctl.USB_DEVICE_CAPABILITY_SUPERSPEED_USB => "SuperSpeed USB",
        UsbIoctl.USB_DEVICE_CAPABILITY_CONTAINER_ID => "Container ID",
        UsbIoctl.USB_DEVICE_CAPABILITY_PLATFORM => "Platform",
        UsbIoctl.USB_DEVICE_CAPABILITY_POWER_DELIVERY => "USB Power Delivery",
        UsbIoctl.USB_DEVICE_CAPABILITY_BATTERY_INFO => "Battery Info",
        UsbIoctl.USB_DEVICE_CAPABILITY_SUPERSPEEDPLUS_USB => "SuperSpeedPlus USB",
        UsbIoctl.USB_DEVICE_CAPABILITY_BILLBOARD => "Billboard",
        UsbIoctl.USB_DEVICE_CAPABILITY_BILLBOARD_EX => "Billboard AUM",
        _ => $"Capability 0x{type:X2}",
    };

    private static string? SummarizeCapability(byte type, byte[] raw)
    {
        switch (type)
        {
            case UsbIoctl.USB_DEVICE_CAPABILITY_SUPERSPEED_USB when raw.Length >= 10:
                int speeds = raw[6] | (raw[7] << 8);
                var supported = new List<string>();
                if ((speeds & 0x01) != 0) supported.Add("Low");
                if ((speeds & 0x02) != 0) supported.Add("Full");
                if ((speeds & 0x04) != 0) supported.Add("High");
                if ((speeds & 0x08) != 0) supported.Add("SuperSpeed");
                return "Supported speeds: " + string.Join(", ", supported);

            case UsbIoctl.USB_DEVICE_CAPABILITY_SUPERSPEEDPLUS_USB:
                return "SuperSpeedPlus (USB 3.1/3.2) capable";

            case UsbIoctl.USB_DEVICE_CAPABILITY_BILLBOARD:
                return raw.Length >= 5 ? $"Billboard: {raw[4]} alternate mode(s) advertised" : "Billboard capability";

            case UsbIoctl.USB_DEVICE_CAPABILITY_CONTAINER_ID when raw.Length >= 20:
                return "ContainerID: " + new Guid(raw.AsSpan(4, 16)).ToString("B");

            default:
                return null;
        }
    }
}
