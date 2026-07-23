namespace UsbInspector.Core.Models;

/// <summary>How confidently a physical-port grouping was derived (higher = more authoritative).</summary>
public enum PortGroupSource
{
    /// <summary>Grouped by the ACPI _PLD connector token surfaced in the location path (highest fidelity).</summary>
    AcpiPld,

    /// <summary>Grouped by the USB4 fabric port (domain + host-router lane adapter).</summary>
    Usb4FabricPort,

    /// <summary>Grouped by a shared DEVPKEY_Device_LocationPaths prefix (approximate).</summary>
    LocationPathPrefix,

    /// <summary>Grouped by DEVPKEY_Device_LocationInfo (last-resort, approximate).</summary>
    LocationInfo,
}

/// <summary>
/// A set of logically-separate devnodes that share the same physical host port / connector — for
/// example the USB 3.x (xHCI) function and the USB4 function routed through one USB-C receptacle.
/// Fidelity is firmware-dependent and labelled via <see cref="Source"/>.
/// </summary>
public sealed class PhysicalPortGroup
{
    /// <summary>The computed key that identifies this physical port.</summary>
    public string PortKey { get; set; } = string.Empty;

    /// <summary>A representative, human-readable name derived from the group members.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How the grouping was derived (confidence/source label).</summary>
    public PortGroupSource Source { get; set; }

    /// <summary>A short human-readable description of the grouping source/confidence.</summary>
    public string SourceLabel => Source switch
    {
        PortGroupSource.AcpiPld => "grouped by ACPI _PLD connector",
        PortGroupSource.Usb4FabricPort => "grouped by USB4 fabric port",
        PortGroupSource.LocationPathPrefix => "grouped by location path (approximate)",
        PortGroupSource.LocationInfo => "grouped by location info (approximate)",
        _ => "grouped (approximate)",
    };

    public List<PhysicalDeviceMember> Members { get; } = new();
}
