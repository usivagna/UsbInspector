namespace UsbInspector.Core.Models;

/// <summary>Parsed USB device descriptor (18 bytes, type 0x01).</summary>
public sealed class UsbDeviceDescriptorInfo
{
    public ushort BcdUsb { get; init; }
    public byte DeviceClass { get; init; }
    public byte DeviceSubClass { get; init; }
    public byte DeviceProtocol { get; init; }
    public byte MaxPacketSize0 { get; init; }
    public ushort IdVendor { get; init; }
    public ushort IdProduct { get; init; }
    public ushort BcdDevice { get; init; }
    public byte ManufacturerIndex { get; init; }
    public byte ProductIndex { get; init; }
    public byte SerialNumberIndex { get; init; }
    public byte NumConfigurations { get; init; }

    public string? Manufacturer { get; set; }
    public string? Product { get; set; }
    public string? SerialNumber { get; set; }

    public string UsbVersion => $"{BcdUsb >> 8}.{(BcdUsb >> 4) & 0xF}{BcdUsb & 0xF}".TrimEnd('0').TrimEnd('.');
    public string VendorIdHex => $"0x{IdVendor:X4}";
    public string ProductIdHex => $"0x{IdProduct:X4}";
}

/// <summary>Parsed USB configuration descriptor and its interface tree.</summary>
public sealed class UsbConfigurationDescriptorInfo
{
    public byte ConfigurationValue { get; init; }
    public byte NumInterfaces { get; init; }
    public byte Attributes { get; init; }
    public byte MaxPowerRaw { get; init; }
    public byte ConfigurationStringIndex { get; init; }
    public string? Description { get; set; }

    public bool SelfPowered => (Attributes & 0x40) != 0;
    public bool RemoteWakeup => (Attributes & 0x20) != 0;
    public int MaxPowerMilliAmps => MaxPowerRaw * 2; // USB 2.0 units of 2 mA

    public List<UsbInterfaceDescriptorInfo> Interfaces { get; } = new();
}

/// <summary>Parsed USB interface descriptor (type 0x04).</summary>
public sealed class UsbInterfaceDescriptorInfo
{
    public byte InterfaceNumber { get; init; }
    public byte AlternateSetting { get; init; }
    public byte NumEndpoints { get; init; }
    public byte InterfaceClass { get; init; }
    public byte InterfaceSubClass { get; init; }
    public byte InterfaceProtocol { get; init; }
    public byte InterfaceStringIndex { get; init; }
    public string? Description { get; set; }
    public string? ClassName { get; set; }

    public List<UsbEndpointDescriptorInfo> Endpoints { get; } = new();
}

/// <summary>Parsed USB endpoint descriptor (type 0x05).</summary>
public sealed class UsbEndpointDescriptorInfo
{
    public byte EndpointAddress { get; init; }
    public byte Attributes { get; init; }
    public ushort MaxPacketSize { get; init; }
    public byte Interval { get; init; }

    public int EndpointNumber => EndpointAddress & 0x0F;
    public string Direction => (EndpointAddress & 0x80) != 0 ? "IN" : "OUT";
    public string TransferType => (Attributes & 0x03) switch
    {
        0 => "Control",
        1 => "Isochronous",
        2 => "Bulk",
        3 => "Interrupt",
        _ => "Unknown",
    };
}

/// <summary>A single localized USB string descriptor.</summary>
public sealed class UsbStringDescriptorInfo
{
    public byte Index { get; init; }
    public ushort LanguageId { get; init; }
    public string Value { get; init; } = string.Empty;
}

/// <summary>A device capability descriptor found inside the BOS descriptor (USB 3.x).</summary>
public sealed class UsbBosCapabilityInfo
{
    public byte CapabilityType { get; init; }
    public string CapabilityName { get; init; } = string.Empty;
    public byte[] RawData { get; init; } = Array.Empty<byte>();
    public string? Summary { get; set; }
}
