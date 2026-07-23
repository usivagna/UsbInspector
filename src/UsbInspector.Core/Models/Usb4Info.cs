namespace UsbInspector.Core.Models;

/// <summary>
/// USB4 information for a device, connector, or host router, assembled from the public surfaces
/// Windows exposes (USB4 host-router devnodes and their PnP/DEVPKEY properties, plus tunnelled
/// USB 3.2 speed flags from USB_NODE_CONNECTION_INFORMATION_EX_V2). Full USB4 fabric detail
/// (router/adapter topology, PCIe/DisplayPort tunnelling, per-lane negotiation, native 40 Gbps
/// link state) is not exposed to user mode by public Windows APIs and is labelled accordingly.
/// </summary>
public sealed class Usb4Info
{
    /// <summary>True if the node is a USB4 host router or a USB4-capable device.</summary>
    public bool IsUsb4Capable { get; set; }

    /// <summary>True if this info describes a USB4 host router devnode.</summary>
    public bool IsHostRouter { get; set; }

    /// <summary>Friendly name of the USB4 host router, where applicable.</summary>
    public string? RouterName { get; set; }

    /// <summary>PnP instance id of the USB4 host router devnode, where applicable.</summary>
    public string? HostRouterInstanceId { get; set; }

    /// <summary>Highest USB4/tunnelled link speed label (e.g. "USB4 40 Gbps").</summary>
    public string? LinkSpeedLabel { get; set; }

    /// <summary>True when the device is operating over a tunnelled USB 3.2 (10/20 Gbps) link.</summary>
    public bool SupportsTunnelledUsb { get; set; }

    /// <summary>Human-readable USB4 capabilities discovered from PnP/connection data.</summary>
    public List<string> Capabilities { get; } = new();

    /// <summary>Notes about USB4 data that Windows does not expose to user mode.</summary>
    public List<string> Limitations { get; } = new();

    // ---- USB4 fabric detail (populated from the USB4 connection-manager ETW rundown; admin only) ----

    /// <summary>USB4 domain ID (e.g. 0x202), from the connection-manager rundown.</summary>
    public uint? DomainId { get; set; }

    /// <summary>7-byte USB4 topology ID formatted as "a:b:c:d:e:f:g".</summary>
    public string? TopologyId { get; set; }

    /// <summary>Silicon vendor ID (router VendorId).</summary>
    public ushort? SiliconVendorId { get; set; }

    /// <summary>Silicon product ID (router ProductId).</summary>
    public ushort? SiliconProductId { get; set; }

    /// <summary>Silicon revision, where the rundown reports it.</summary>
    public uint? SiliconRevision { get; set; }

    /// <summary>USB4 specification version (e.g. "1.0").</summary>
    public string? Usb4Version { get; set; }

    /// <summary>Router UUID (ROUTER_CS_7/8), where reported.</summary>
    public string? Uuid { get; set; }

    /// <summary>ASCII vendor name from the USB4 DROM.</summary>
    public string? AsciiVendorName { get; set; }

    /// <summary>ASCII model name from the USB4 DROM.</summary>
    public string? AsciiModelName { get; set; }

    /// <summary>Unit (product) vendor ID (DeviceID / idVendor of the product descriptor).</summary>
    public ushort? UnitVendorId { get; set; }

    /// <summary>Unit (product) product ID (ModelID / idProduct of the product descriptor).</summary>
    public ushort? UnitProductId { get; set; }

    /// <summary>Device firmware version, where reported.</summary>
    public string? FirmwareVersion { get; set; }

    /// <summary>Computed downstream bandwidth in Gbps (from the upstream port link speed/width).</summary>
    public double? CurrentBandwidthDownGbps { get; set; }

    /// <summary>Computed upstream bandwidth in Gbps.</summary>
    public double? CurrentBandwidthUpGbps { get; set; }

    /// <summary>USB4 link generation (2 or 3), where computable.</summary>
    public int? LinkGeneration { get; set; }

    /// <summary>True when the port lanes are bonded (dual-lane).</summary>
    public bool? LanesBonded { get; set; }

    /// <summary>Total DP-IN adapters on this router, where reported.</summary>
    public int? DpInAdaptersTotal { get; set; }

    /// <summary>Tunnelled DP-IN adapters on this router.</summary>
    public int? DpInAdaptersTunneled { get; set; }

    /// <summary>Unavailable DP-IN adapters on this router.</summary>
    public int? DpInAdaptersUnavailable { get; set; }

    /// <summary>True when full fabric detail was unavailable because the process was not elevated.</summary>
    public bool FabricDetailRequiresElevation { get; set; }

    public bool HasFabricData =>
        DomainId is not null
        || TopologyId is not null
        || SiliconVendorId is not null
        || AsciiModelName is not null
        || CurrentBandwidthDownGbps is not null;

    public bool HasAnyData =>
        IsUsb4Capable
        || IsHostRouter
        || RouterName is not null
        || LinkSpeedLabel is not null
        || SupportsTunnelledUsb
        || Capabilities.Count > 0
        || HasFabricData;
}
