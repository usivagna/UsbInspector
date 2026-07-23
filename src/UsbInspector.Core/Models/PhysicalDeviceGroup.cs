namespace UsbInspector.Core.Models;

/// <summary>A lightweight reference to a devnode that belongs to a physical device group.</summary>
public sealed class PhysicalDeviceMember
{
    public string Name { get; set; } = string.Empty;
    public UsbNodeKind Kind { get; set; }
    public string? InstanceId { get; set; }
}

/// <summary>
/// A set of logically-separate devnodes that Windows reports as a single physical
/// device/enclosure, grouped by <c>DEVPKEY_Device_ContainerId</c>. This is how the OS relates
/// otherwise-independent USB3 and USB4 functions that live behind the same physical port/connector
/// (e.g. a USB-C dock exposing both a USB 3.x hub and a USB4 host router).
/// </summary>
public sealed class PhysicalDeviceGroup
{
    public string ContainerId { get; set; } = string.Empty;

    /// <summary>A representative, human-readable name derived from the group members.</summary>
    public string Name { get; set; } = string.Empty;

    public List<PhysicalDeviceMember> Members { get; } = new();
}
