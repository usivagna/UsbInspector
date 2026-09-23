namespace UsbInspector.Core.Models;

public enum ChassisSide
{
    Unassigned,
    Front,
    Rear,
    Other,
}

public sealed record PortAssignment(ChassisSide Location, string Label);

/// <summary>A logical hub port, not a firmware-verified chassis receptacle.</summary>
public sealed class PortMapEntry
{
    public required UsbNode Node { get; init; }
    public required string Route { get; init; }
    public string? Key { get; init; }
    public bool IsConnected => Node.Kind is UsbNodeKind.Device or UsbNodeKind.ExternalHub;
}
