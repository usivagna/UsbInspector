namespace UsbInspector.Core.Models;

/// <summary>A complete point-in-time snapshot of the system's USB topology.</summary>
public sealed class UsbSnapshot
{
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string MachineName { get; init; } = Environment.MachineName;
    public string OsVersion { get; init; } = Environment.OSVersion.VersionString;
    public bool IsElevated { get; init; }

    /// <summary>System-wide charging / USB-C Power Delivery state, where the OS exposes it.</summary>
    public Services.PowerDeliveryInfo? PowerDelivery { get; set; }

    /// <summary>Root of the topology: one node per USB host controller.</summary>
    public List<UsbNode> HostControllers { get; } = new();

    /// <summary>USB4 host router devnodes discovered on the system (a separate top-level section).</summary>
    public List<UsbNode> Usb4HostRouters { get; } = new();

    /// <summary>Global, non-fatal issues encountered during the scan.</summary>
    public List<string> Warnings { get; } = new();

    /// <summary>Flattens the tree into every node, depth-first.</summary>
    public IEnumerable<UsbNode> EnumerateAll()
    {
        IEnumerable<UsbNode> Walk(UsbNode node)
        {
            yield return node;
            foreach (UsbNode child in node.Children)
            {
                foreach (UsbNode descendant in Walk(child))
                {
                    yield return descendant;
                }
            }
        }

        return HostControllers.Concat(Usb4HostRouters).SelectMany(Walk);
    }

    public int DeviceCount => EnumerateAll().Count(n => n.Kind == UsbNodeKind.Device);
    public int HubCount => EnumerateAll().Count(n => n.Kind is UsbNodeKind.RootHub or UsbNodeKind.ExternalHub);
    public int Usb4RouterCount => Usb4HostRouters.Count;
}
