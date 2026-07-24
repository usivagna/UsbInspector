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

    /// <summary>
    /// Groups otherwise-independent devnodes that Windows reports as one physical device/enclosure,
    /// keyed by <c>DEVPKEY_Device_ContainerId</c>. Only containers spanning more than one devnode are
    /// returned (a single-function container is not a meaningful "group"). Serialized with the
    /// snapshot so JSON exports include the physical grouping.
    /// </summary>
    public IReadOnlyList<PhysicalDeviceGroup> PhysicalDevices => BuildPhysicalGroups();

    /// <summary>
    /// Groups devnodes by the physical host port / connector they share (e.g. the USB 3.x and USB4
    /// functions on one USB-C receptacle), using the best available Windows signal. Firmware-dependent
    /// and labelled with its confidence source. Serialized with the snapshot for JSON export.
    /// </summary>
    public IReadOnlyList<PhysicalPortGroup> PhysicalPorts => BuildPhysicalPortGroups();

    /// <summary>Clusters all nodes sharing a physical port/connector into groups.</summary>
    public IReadOnlyList<PhysicalPortGroup> BuildPhysicalPortGroups() =>
        new Services.PhysicalPortGrouper().Group(this);

    /// <summary>The all-zero GUID Windows assigns to devnodes that have no real container.</summary>
    public static bool IsRealContainerId(string? containerId)
    {
        if (string.IsNullOrWhiteSpace(containerId))
        {
            return false;
        }

        return !Guid.TryParse(containerId, out Guid g) || g != Guid.Empty;
    }

    /// <summary>Clusters all nodes sharing a real ContainerId into physical-device groups.</summary>
    public IReadOnlyList<PhysicalDeviceGroup> BuildPhysicalGroups()
    {
        var groups = new Dictionary<string, PhysicalDeviceGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (UsbNode node in EnumerateAll())
        {
            string? containerId = node.Pnp?.ContainerId;
            if (!IsRealContainerId(containerId))
            {
                continue;
            }

            if (!groups.TryGetValue(containerId!, out PhysicalDeviceGroup? group))
            {
                group = new PhysicalDeviceGroup { ContainerId = containerId! };
                groups[containerId!] = group;
            }

            group.Members.Add(new PhysicalDeviceMember
            {
                Name = node.Name,
                Kind = node.Kind,
                InstanceId = node.InstanceId,
            });
        }

        var result = groups.Values.Where(g => g.Members.Count > 1).ToList();
        foreach (PhysicalDeviceGroup group in result)
        {
            group.Name = DeriveGroupName(group);
        }

        return result;
    }

    private static string DeriveGroupName(PhysicalDeviceGroup group)
    {
        // Prefer a real device/hub/router name over infrastructure (host controllers) or generic entries.
        PhysicalDeviceMember best =
            group.Members.FirstOrDefault(m => m.Kind is UsbNodeKind.Device or UsbNodeKind.ExternalHub
                                              or UsbNodeKind.Usb4HostRouter)
            ?? group.Members[0];
        return best.Name;
    }
}
