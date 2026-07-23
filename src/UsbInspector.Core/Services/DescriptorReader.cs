using Microsoft.Win32.SafeHandles;
using System.Text;
using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;

namespace UsbInspector.Core.Services;

/// <summary>
/// Reads USB descriptors from a device on a hub port using
/// IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION.
/// </summary>
public sealed class DescriptorReader
{
    private const int RequestHeaderSize = 12;   // ConnectionIndex(4) + SetupPacket(8)
    private const byte UsbRequestGetDescriptor = 0x06;
    private const byte BmRequestDeviceToHostStandardDevice = 0x80;

    private readonly SafeFileHandle _hub;

    public DescriptorReader(SafeFileHandle hub) => _hub = hub;

    /// <summary>Requests a descriptor and returns just the payload bytes, or null on failure.</summary>
    public byte[]? GetDescriptor(uint connectionIndex, byte descriptorType, byte descriptorIndex, ushort languageId, int length)
    {
        int bufferSize = RequestHeaderSize + length;
        var buffer = new byte[bufferSize];

        // USB_DESCRIPTOR_REQUEST header
        BitConverter.TryWriteBytes(buffer.AsSpan(0, 4), connectionIndex);
        buffer[4] = BmRequestDeviceToHostStandardDevice;         // bmRequest
        buffer[5] = UsbRequestGetDescriptor;                     // bRequest
        buffer[6] = descriptorIndex;                             // wValue low
        buffer[7] = descriptorType;                              // wValue high
        BitConverter.TryWriteBytes(buffer.AsSpan(8, 2), languageId);   // wIndex
        BitConverter.TryWriteBytes(buffer.AsSpan(10, 2), (ushort)length); // wLength

        if (!NativeDevice.Control(_hub, UsbIoctl.IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION, buffer, out uint returned))
        {
            return null;
        }

        int payloadLength = (int)returned - RequestHeaderSize;
        if (payloadLength <= 0)
        {
            return null;
        }

        return buffer.AsSpan(RequestHeaderSize, payloadLength).ToArray();
    }

    public UsbDeviceDescriptorInfo? ReadDeviceDescriptor(uint connectionIndex)
    {
        byte[]? data = GetDescriptor(connectionIndex, UsbIoctl.USB_DEVICE_DESCRIPTOR_TYPE, 0, 0, 18);
        return data is null ? null : DescriptorParser.ParseDevice(data);
    }

    public UsbConfigurationDescriptorInfo? ReadConfiguration(uint connectionIndex, byte configIndex)
    {
        // First read the 9-byte header to learn wTotalLength.
        byte[]? header = GetDescriptor(connectionIndex, UsbIoctl.USB_CONFIGURATION_DESCRIPTOR_TYPE, configIndex, 0, 9);
        if (header is null || header.Length < 9)
        {
            return null;
        }

        int totalLength = header[2] | (header[3] << 8);
        if (totalLength <= 9)
        {
            return DescriptorParser.ParseConfiguration(header);
        }

        byte[]? full = GetDescriptor(connectionIndex, UsbIoctl.USB_CONFIGURATION_DESCRIPTOR_TYPE, configIndex, 0, totalLength);
        return DescriptorParser.ParseConfiguration(full ?? header);
    }

    /// <summary>Returns the list of language IDs the device supports (string descriptor index 0).</summary>
    public ushort[] ReadSupportedLanguages(uint connectionIndex)
    {
        byte[]? data = GetDescriptor(connectionIndex, UsbIoctl.USB_STRING_DESCRIPTOR_TYPE, 0, 0, 255);
        if (data is null || data.Length < 4)
        {
            return Array.Empty<ushort>();
        }

        int count = (data[0] - 2) / 2;
        var langs = new List<ushort>();
        for (int i = 0; i < count; i++)
        {
            int off = 2 + (i * 2);
            if (off + 1 < data.Length)
            {
                langs.Add((ushort)(data[off] | (data[off + 1] << 8)));
            }
        }

        return langs.ToArray();
    }

    public string? ReadStringDescriptor(uint connectionIndex, byte index, ushort languageId)
    {
        if (index == 0)
        {
            return null;
        }

        byte[]? data = GetDescriptor(connectionIndex, UsbIoctl.USB_STRING_DESCRIPTOR_TYPE, index, languageId, 255);
        if (data is null || data.Length < 2 || data[1] != UsbIoctl.USB_STRING_DESCRIPTOR_TYPE)
        {
            return null;
        }

        int stringBytes = Math.Min(data[0], data.Length) - 2;
        if (stringBytes <= 0)
        {
            return string.Empty;
        }

        return Encoding.Unicode.GetString(data, 2, stringBytes).TrimEnd('\0');
    }

    public List<UsbBosCapabilityInfo> ReadBos(uint connectionIndex)
    {
        byte[]? header = GetDescriptor(connectionIndex, UsbIoctl.USB_BOS_DESCRIPTOR_TYPE, 0, 0, 5);
        if (header is null || header.Length < 5)
        {
            return new List<UsbBosCapabilityInfo>();
        }

        int totalLength = header[2] | (header[3] << 8);
        if (totalLength <= 5)
        {
            return DescriptorParser.ParseBos(header);
        }

        byte[]? full = GetDescriptor(connectionIndex, UsbIoctl.USB_BOS_DESCRIPTOR_TYPE, 0, 0, totalLength);
        return DescriptorParser.ParseBos(full ?? header);
    }
}
