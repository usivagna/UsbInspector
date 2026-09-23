using UsbInspector.Core.Models;

namespace UsbInspector.Core.Services;

/// <summary>Uses controller identity and the full hub route; never infers front/rear from port numbers.</summary>
public static class PortMapBuilder
{
    public static IReadOnlyList<PortMapEntry> Build(UsbSnapshot snapshot)
    {
        var entries = new List<PortMapEntry>();
        foreach (UsbNode controller in snapshot.HostControllers)
        {
            string? key = string.IsNullOrWhiteSpace(controller.InstanceId)
                ? null
                : $"{Uri.EscapeDataString(snapshot.MachineName)}/{Uri.EscapeDataString(controller.InstanceId)}";
            UsbNode[] roots = controller.Children.Where(n => n.Kind == UsbNodeKind.RootHub).ToArray();
            for (int i = 0; i < roots.Length; i++)
            {
                // Multiple unidentified root hubs cannot safely share persistent labels.
                Walk(roots[i], roots.Length == 1 ? key : null,
                    $"{controller.Name} [{controller.InstanceId ?? "unknown controller"}] / Root hub {i + 1}", entries);
            }
        }

        return entries;
    }

    private static void Walk(UsbNode hub, string? key, string route, List<PortMapEntry> entries)
    {
        foreach (UsbNode node in hub.Children)
        {
            if (node.PortNumber is not uint port)
            {
                continue;
            }

            string? portKey = key is null ? null : $"{key}/port:{port}";
            string portRoute = $"{route} / Port {port}";
            entries.Add(new PortMapEntry { Node = node, Key = portKey, Route = portRoute });

            if (node.Kind == UsbNodeKind.ExternalHub)
            {
                // A replacement hub must not inherit the previous hub's downstream labels.
                string? hubKey = portKey is null || string.IsNullOrWhiteSpace(node.InstanceId)
                    ? null
                    : $"{portKey}/hub:{Uri.EscapeDataString(node.InstanceId)}";
                Walk(node, hubKey, $"{portRoute} / {node.Name}", entries);
            }
        }
    }
}
