using UsbInspector.Core.Models;

namespace UsbInspector.Core.Services;

/// <summary>
/// Pure helpers for deriving a physical-port ("connector") key from Windows location properties.
/// Firmware-dependent and best-effort — see <see cref="PhysicalPortGroup.SourceLabel"/> for labelling.
/// </summary>
public static class PortLocation
{
    /// <summary>
    /// Extracts an ACPI <c>_PLD</c>/connector token from a location path, if one is present. Location
    /// paths for ports backed by an ACPI connector expose an <c>ACPI(...)</c> segment whose value is the
    /// firmware connector name shared by every function on that physical receptacle.
    /// </summary>
    public static string? AcpiConnectorToken(IEnumerable<string>? locationPaths)
    {
        if (locationPaths is null)
        {
            return null;
        }

        foreach (string path in locationPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            int idx = path.IndexOf("ACPI(", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                continue;
            }

            int open = idx + "ACPI(".Length;
            int close = path.IndexOf(')', open);
            if (close > open)
            {
                return "ACPI:" + path.Substring(open, close - open);
            }
        }

        return null;
    }

    /// <summary>
    /// Derives a connector prefix key from a location path by dropping the final device segment, so that
    /// functions sharing the same upstream port/connector collapse to one key. Returns null when there is
    /// no usable path.
    /// </summary>
    public static string? ConnectorPrefix(IEnumerable<string>? locationPaths)
    {
        string? path = locationPaths?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
        if (path is null)
        {
            return null;
        }

        string[] segments = path.Split('#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length <= 1)
        {
            return path.Trim();
        }

        return string.Join("#", segments.Take(segments.Length - 1));
    }
}

/// <summary>
/// Clusters devnodes by the physical host port / connector they sit on, using the best available
/// Windows signal (ACPI connector token → location-path prefix → location info). Only ports with more
/// than one function are returned. Fidelity is firmware-dependent and captured in each group's source.
/// </summary>
public sealed class PhysicalPortGrouper
{
    public IReadOnlyList<PhysicalPortGroup> Group(UsbSnapshot snapshot)
    {
        var groups = new Dictionary<string, PhysicalPortGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (UsbNode node in snapshot.EnumerateAll())
        {
            (string key, PortGroupSource source)? computed = ComputePortKey(node);
            if (computed is null)
            {
                continue;
            }

            (string key, PortGroupSource source) = computed.Value;

            if (!groups.TryGetValue(key, out PhysicalPortGroup? group))
            {
                group = new PhysicalPortGroup { PortKey = key, Source = source };
                groups[key] = group;
            }
            else if (source < group.Source)
            {
                // Prefer the higher-confidence source seen for this key.
                group.Source = source;
            }

            group.Members.Add(new PhysicalDeviceMember
            {
                Name = node.Name,
                Kind = node.Kind,
                InstanceId = node.InstanceId,
            });
        }

        var result = groups.Values.Where(g => g.Members.Count > 1).ToList();
        foreach (PhysicalPortGroup group in result)
        {
            group.Name = DeriveName(group);
        }

        return result;
    }

    /// <summary>Computes the highest-confidence physical-port key available for a node.</summary>
    public static (string Key, PortGroupSource Source)? ComputePortKey(UsbNode node)
    {
        PnpDeviceProperties? pnp = node.Pnp;

        // 1) USB4 fabric port: same domain + topology parent share a physical fabric port.
        if (node.Usb4 is { DomainId: uint domain, TopologyId: string topo } && !string.IsNullOrEmpty(topo))
        {
            return ($"USB4:{domain:X}:{topo}", PortGroupSource.Usb4FabricPort);
        }

        // 2) ACPI _PLD / connector token (highest fidelity for real connectors).
        string? acpi = PortLocation.AcpiConnectorToken(pnp?.LocationPaths);
        if (acpi is not null)
        {
            return (acpi, PortGroupSource.AcpiPld);
        }

        // 3) Location-path prefix (approximate).
        string? prefix = PortLocation.ConnectorPrefix(pnp?.LocationPaths);
        if (prefix is not null)
        {
            return ("PATH:" + prefix, PortGroupSource.LocationPathPrefix);
        }

        // 4) Location info (last resort).
        if (!string.IsNullOrWhiteSpace(pnp?.LocationInfo))
        {
            return ("LOC:" + pnp!.LocationInfo, PortGroupSource.LocationInfo);
        }

        return null;
    }

    private static string DeriveName(PhysicalPortGroup group)
    {
        PhysicalDeviceMember best =
            group.Members.FirstOrDefault(m => m.Kind is UsbNodeKind.Device or UsbNodeKind.ExternalHub
                                              or UsbNodeKind.Usb4DeviceRouter or UsbNodeKind.Usb4HostRouter)
            ?? group.Members[0];
        return best.Name;
    }
}
