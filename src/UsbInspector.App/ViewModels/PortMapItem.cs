using UsbInspector.Core.Models;

namespace UsbInspector_App.ViewModels;

public sealed class PortMapItem(PortMapEntry entry, PortAssignment? assignment)
{
    public PortMapEntry Entry { get; } = entry;
    public UsbNode Node => Entry.Node;
    public ChassisSide Side => assignment?.Location ?? ChassisSide.Unassigned;
    public string PortLabel => string.IsNullOrWhiteSpace(assignment?.Label)
        ? $"Port {Node.PortNumber}"
        : assignment.Label;
    public string SavedLabel => assignment?.Label ?? string.Empty;
    public string LocationLabel => $"{Side} · {PortLabel}";
    public string Name => Node.Name;
    public string Glyph => new UsbTreeItem(Node, Array.Empty<UsbTreeItem>()).Glyph;
    public string Route => Entry.Route;
    public string Identity => string.Join(" · ", Node.StorageDevices.SelectMany(d => d.Volumes)
        .Select(v => v.DriveLetter).Where(s => !string.IsNullOrWhiteSpace(s))
        .Concat(Node.StorageDevices.Select(d => d.SerialNumber)
            .Append(Node.DeviceDescriptor?.SerialNumber).Where(s => !string.IsNullOrWhiteSpace(s)))
        .Distinct(StringComparer.OrdinalIgnoreCase));
    public string Connection => Entry.IsConnected ? "Connected" :
        Node.Power?.ConnectionStatus == UsbPortConnectionStatus.NoDeviceConnected ? "Empty" : "Connection unavailable / error";
}
