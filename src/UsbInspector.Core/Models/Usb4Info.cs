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

    public bool HasAnyData =>
        IsUsb4Capable
        || IsHostRouter
        || RouterName is not null
        || LinkSpeedLabel is not null
        || SupportsTunnelledUsb
        || Capabilities.Count > 0;
}
