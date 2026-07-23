namespace UsbInspector.Core.Models;

/// <summary>
/// USB Type-C / Power Delivery information for a device or connector, assembled from the
/// public surfaces Windows exposes (UCM connectors, EX_V2 speed flags, BOS Billboard/SSP
/// capabilities, and OS battery/charging state).
/// </summary>
public sealed class UsbCInfo
{
    /// <summary>True if the device/connector is known to be a USB Type-C connector.</summary>
    public bool IsTypeCConnector { get; set; }

    public string? ConnectorPartner { get; set; }
    public string? DataRole { get; set; }
    public string? PowerRole { get; set; }

    /// <summary>Highest negotiated USB signalling speed (e.g. "Gen 2x2 (20 Gbps)").</summary>
    public string? NegotiatedLinkSpeed { get; set; }

    /// <summary>Lane/gen capabilities reported by USB_NODE_CONNECTION_INFORMATION_EX_V2.</summary>
    public List<string> SupportedLinkModes { get; } = new();

    /// <summary>Alternate modes advertised via the USB Billboard capability (e.g. DisplayPort).</summary>
    public List<UsbAltModeInfo> AlternateModes { get; } = new();

    public bool SupportsSuperSpeedPlus { get; set; }
    public bool HasBillboardCapability { get; set; }

    /// <summary>Negotiated charging power in watts, where the OS exposes it; otherwise null.</summary>
    public double? PowerDeliveryWatts { get; set; }
    public string? ChargingState { get; set; }

    /// <summary>Human-readable notes about data that Windows does not expose to user mode.</summary>
    public List<string> Limitations { get; } = new();

    public bool HasAnyData =>
        IsTypeCConnector
        || NegotiatedLinkSpeed is not null
        || SupportedLinkModes.Count > 0
        || AlternateModes.Count > 0
        || SupportsSuperSpeedPlus
        || HasBillboardCapability
        || PowerDeliveryWatts is not null
        || ChargingState is not null;
}

/// <summary>A single USB-C alternate mode advertised by a Billboard device.</summary>
public sealed class UsbAltModeInfo
{
    public ushort SVID { get; init; }
    public uint AlternateModeVdo { get; init; }
    public byte Index { get; init; }
    public string? Name { get; set; }

    public string SvidHex => $"0x{SVID:X4}";
}
