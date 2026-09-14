namespace UsbInspector.Core.Models;

/// <summary>A physical disk attached to this USB devnode, not an aggregate of its children.</summary>
public sealed class UsbStorageInfo
{
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? DeviceId { get; set; }
    public ulong? CapacityBytes { get; set; }
    public bool? IsReadOnly { get; set; }
    public List<UsbVolumeInfo> Volumes { get; } = new();
}

/// <summary>A logical volume reached through the physical disk's WMI partition associations.</summary>
public sealed class UsbVolumeInfo
{
    public string? DriveLetter { get; set; }
    public string? Label { get; set; }
    public string? FileSystem { get; set; }
    public ulong? CapacityBytes { get; set; }
}
