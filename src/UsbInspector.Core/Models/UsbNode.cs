namespace UsbInspector.Core.Models;

/// <summary>Power / port electrical information for a device on a hub port.</summary>
public sealed class PortPowerInfo
{
    public int? RequiredMilliAmps { get; set; }
    public bool? SelfPowered { get; set; }
    public bool? RemoteWakeupCapable { get; set; }
    public UsbPortConnectionStatus? ConnectionStatus { get; set; }
}

/// <summary>
/// A node in the USB topology tree: a host controller, a hub (root or external),
/// a connected device, or an empty port. Detail sections are populated where available.
/// </summary>
public sealed class UsbNode
{
    public UsbNodeKind Kind { get; set; } = UsbNodeKind.Unknown;

    /// <summary>Display name shown in the tree.</summary>
    public string Name { get; set; } = string.Empty;

    public string? InstanceId { get; set; }
    public string? DevicePath { get; set; }
    public uint DevInst { get; set; }

    /// <summary>1-based port number on the parent hub, when applicable.</summary>
    public uint? PortNumber { get; set; }

    public bool IsHub { get; set; }
    public ushort? DeviceAddress { get; set; }
    public UsbSpeed Speed { get; set; } = UsbSpeed.Unknown;

    public UsbDeviceDescriptorInfo? DeviceDescriptor { get; set; }
    public List<UsbConfigurationDescriptorInfo> Configurations { get; } = new();
    public List<UsbStringDescriptorInfo> StringDescriptors { get; } = new();
    public List<UsbBosCapabilityInfo> BosCapabilities { get; } = new();

    public PnpDeviceProperties? Pnp { get; set; }
    public PortPowerInfo? Power { get; set; }
    public UsbCInfo? UsbC { get; set; }

    /// <summary>Non-fatal issues encountered while inspecting this node (e.g. access denied).</summary>
    public List<string> Warnings { get; } = new();

    public List<UsbNode> Children { get; } = new();
}
