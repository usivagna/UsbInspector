using System.Text;
using UsbInspector.Core.Models;

namespace UsbInspector.Core.Services;

/// <summary>
/// Builds a concise, human-readable plain-text summary of a USB snapshot — suitable for copying to
/// the clipboard and pasting into an email or support ticket. Deliberately non-technical: friendly
/// device types, speeds and physical grouping, without hex dumps or raw PnP keys.
/// </summary>
public static class SnapshotReport
{
    public static string Build(UsbSnapshot snapshot)
    {
        var sb = new StringBuilder();

        sb.AppendLine("USB Inspector — device summary");
        sb.AppendLine($"Computer: {snapshot.MachineName}");
        sb.AppendLine($"Windows:  {snapshot.OsVersion}");
        sb.AppendLine($"Captured: {snapshot.CapturedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        int deviceCount = snapshot.DeviceCount;
        int portCount = snapshot.PhysicalPorts.Count;
        sb.AppendLine($"{deviceCount} device(s) connected"
            + (portCount > 0 ? $" across {portCount} physical port(s)." : "."));
        if (snapshot.Usb4RouterCount > 0)
        {
            sb.AppendLine($"{snapshot.Usb4RouterCount} USB4 host router(s) detected.");
        }

        sb.AppendLine();
        sb.AppendLine("Connected devices");
        sb.AppendLine("-----------------");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (UsbNode node in snapshot.EnumerateAll())
        {
            if (node.Kind is not (UsbNodeKind.Device or UsbNodeKind.ExternalHub
                or UsbNodeKind.Usb4HostRouter or UsbNodeKind.Usb4DeviceRouter))
            {
                continue;
            }

            string id = node.InstanceId ?? node.Name;
            if (!seen.Add(id))
            {
                continue;
            }

            string type = UsbDeviceClassifier.FriendlyType(UsbDeviceClassifier.Classify(node));
            string speed = DescribeSpeed(node.Speed);
            sb.Append($"  • {node.Name}  [{type}]");
            if (!string.IsNullOrEmpty(speed))
            {
                sb.Append($"  — {speed}");
            }

            sb.AppendLine();

            Usb4Info? u = node.Usb4;
            if (u is not null && u.HasFabricData && u.CurrentBandwidthDownGbps is double gbps)
            {
                sb.AppendLine($"      USB4 link: {gbps:0} Gbps"
                    + (u.LinkGeneration is int g ? $" (Gen {g}, {(u.LanesBonded == true ? "dual" : "single")} lane)" : string.Empty));
            }
        }

        IReadOnlyList<PhysicalPortGroup> ports = snapshot.PhysicalPorts;
        if (ports.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Devices sharing a physical port");
            sb.AppendLine("-------------------------------");
            foreach (PhysicalPortGroup group in ports)
            {
                sb.AppendLine($"  • {group.Name} ({group.ConfidenceBadge}) — {group.Members.Count} functions:");
                foreach (PhysicalDeviceMember member in group.Members)
                {
                    sb.AppendLine($"      - {member.Name}");
                }
            }
        }

        return sb.ToString();
    }

    private static string DescribeSpeed(UsbSpeed speed) => speed switch
    {
        UsbSpeed.Low => "Low Speed (1.5 Mbps)",
        UsbSpeed.Full => "Full Speed (12 Mbps)",
        UsbSpeed.High => "High Speed (480 Mbps)",
        UsbSpeed.Super => "SuperSpeed (5 Gbps)",
        UsbSpeed.SuperPlus => "SuperSpeed+ (10 Gbps)",
        UsbSpeed.SuperPlus20 => "SuperSpeed+ (20 Gbps)",
        UsbSpeed.Usb4Gen2x2 => "USB4 (20 Gbps)",
        UsbSpeed.Usb4Gen3x2 => "USB4 (40 Gbps)",
        _ => string.Empty,
    };
}
