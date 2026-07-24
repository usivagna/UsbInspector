using System.Globalization;
using UsbInspector.Core.Models;

namespace UsbInspector.Core.Services;

/// <summary>
/// Maps USB4 connection-manager ETW rundown events onto the enumerated host-router nodes and builds a
/// clean Host Router → Device Router topology (mirroring the Windows Settings "USB4 hubs and devices"
/// page). Enrichment is best-effort and additive: when no rundown data is available (e.g. not elevated)
/// the existing PnP subtree is left untouched.
/// </summary>
public sealed class Usb4FabricEnricher
{
    /// <summary>One router (host or device) reconstructed from the rundown, keyed by domain+topology.</summary>
    private sealed class RouterRecord
    {
        public uint DomainId;
        public byte[] TopologyId = Array.Empty<byte>();
        public string TopologyKey = string.Empty;
        public Usb4Event? Info;
        public readonly List<Usb4Event> Ports = new();
        public readonly List<Usb4Event> DpAdapters = new();
        public string? DeviceInstancePath;

        public int? DpInTotal;
        public int? DpInTunneled;
        public int? DpInUnavailable;
        public double? BandwidthGbps;
        public int Generation;
        public int Lanes;
        public string? BandwidthLabel;
    }

    public void Enrich(IReadOnlyList<UsbNode> hostRouters, Usb4RundownData data, List<string> warnings)
    {
        if (data.RequiresElevation)
        {
            foreach (UsbNode router in hostRouters)
            {
                if (router.Usb4 is not null)
                {
                    router.Usb4.FabricDetailRequiresElevation = true;
                }
            }

            return;
        }

        if (!data.HasData)
        {
            return;
        }

        Dictionary<string, RouterRecord> routers = GroupRouters(data);

        foreach (RouterRecord record in routers.Values)
        {
            PopulateFabric(record);
        }

        // Enrich host-router nodes (all-zero topology) and attach their device routers.
        foreach (UsbNode hostNode in hostRouters)
        {
            RouterRecord? hostRecord = MatchHostRecord(hostNode, routers.Values);
            if (hostRecord is null)
            {
                continue;
            }

            ApplyFabric(hostNode, hostRecord);

            IEnumerable<RouterRecord> devices = routers.Values
                .Where(r => r.DomainId == hostRecord.DomainId && !IsAllZero(r.TopologyId))
                .OrderBy(r => r.TopologyKey);

            AttachDeviceRouters(hostNode, devices.ToList());
        }
    }

    private static Dictionary<string, RouterRecord> GroupRouters(Usb4RundownData data)
    {
        var routers = new Dictionary<string, RouterRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (Usb4Event e in data.Events)
        {
            if (e.DomainId is null && e.TopologyId is null)
            {
                continue;
            }

            uint domain = e.DomainId ?? 0;
            byte[] topo = e.TopologyId ?? Array.Empty<byte>();
            string key = domain.ToString("X8") + "|" + Usb4Fabric.FormatTopologyId(topo);

            if (!routers.TryGetValue(key, out RouterRecord? record))
            {
                record = new RouterRecord
                {
                    DomainId = domain,
                    TopologyId = topo,
                    TopologyKey = Usb4Fabric.FormatTopologyId(topo),
                };
                routers[key] = record;
            }

            string name = e.EventName;
            if (name.Contains("DeviceRouterInformation", StringComparison.OrdinalIgnoreCase))
            {
                record.Info = e;
                record.DeviceInstancePath ??= e.DeviceInstancePath;
            }
            else if (name.Contains("PortInformation", StringComparison.OrdinalIgnoreCase))
            {
                record.Ports.Add(e);
            }
            else if (name.Contains("DPAdapterInformation", StringComparison.OrdinalIgnoreCase))
            {
                record.DpAdapters.Add(e);
            }

            record.DeviceInstancePath ??= e.DeviceInstancePath;
        }

        return routers;
    }

    private static void PopulateFabric(RouterRecord record)
    {
        // DP-IN adapter counts: DP adapters that are inputs (IsDPOut == false).
        int total = 0, tunneled = 0, unavailable = 0;
        foreach (Usb4Event dp in record.DpAdapters)
        {
            bool isOut = dp.GetBool("IsDPOut");
            if (isOut)
            {
                continue;
            }

            total++;
            if (dp.GetBool("IsTunneled"))
            {
                tunneled++;
            }
            else if (string.Equals(dp.Get("IsAvailable"), "False", StringComparison.OrdinalIgnoreCase))
            {
                unavailable++;
            }
        }

        record.DpInTotal = record.DpAdapters.Count > 0 ? total : null;
        record.DpInTunneled = record.DpAdapters.Count > 0 ? tunneled : null;
        record.DpInUnavailable = record.DpAdapters.Count > 0 ? unavailable : null;

        // Bandwidth from the upstream (non-DFP) port, if present.
        Usb4Event? upstream = record.Ports.FirstOrDefault(p =>
            string.Equals(p.Get("IsDFP"), "False", StringComparison.OrdinalIgnoreCase))
            ?? record.Ports.FirstOrDefault();

        if (upstream is not null)
        {
            byte speed = ToByte(upstream.Get("CurrentLinkSpeed"));
            byte width = ToByte(upstream.Get("NegotiatedLinkWidth"));
            bool bonded = upstream.GetBool("LaneBonded") || upstream.GetBool("IsLaneBonded");
            Usb4Fabric.LinkBandwidth? bw = Usb4Fabric.ComputeBandwidth(speed, width, bonded);
            if (bw is Usb4Fabric.LinkBandwidth b)
            {
                record.BandwidthGbps = b.Gbps;
                record.Generation = b.Generation;
                record.Lanes = b.Lanes;
                record.BandwidthLabel = b.Label;
            }
        }
    }

    private static RouterRecord? MatchHostRecord(UsbNode hostNode, IEnumerable<RouterRecord> records)
    {
        string? instanceId = hostNode.Usb4?.HostRouterInstanceId ?? hostNode.InstanceId;

        // Prefer an exact device-instance-path match; else fall back to the all-zero (host) topology.
        RouterRecord? byPath = records.FirstOrDefault(r =>
            r.DeviceInstancePath is not null
            && instanceId is not null
            && string.Equals(r.DeviceInstancePath, instanceId, StringComparison.OrdinalIgnoreCase));

        return byPath ?? records.FirstOrDefault(r => IsAllZero(r.TopologyId) && r.Info is not null);
    }

    private void AttachDeviceRouters(UsbNode hostNode, List<RouterRecord> devices)
    {
        if (devices.Count == 0)
        {
            return;
        }

        // Build synthetic device-router nodes keyed by topology, nesting by topology parent.
        var nodes = new Dictionary<string, UsbNode>(StringComparer.OrdinalIgnoreCase);

        foreach (RouterRecord record in devices)
        {
            nodes[record.TopologyKey] = BuildDeviceRouterNode(record);
        }

        foreach (RouterRecord record in devices)
        {
            byte[]? parentTopo = Usb4Fabric.ParentTopologyId(record.TopologyId);
            string parentKey = parentTopo is null ? string.Empty : Usb4Fabric.FormatTopologyId(parentTopo);
            UsbNode node = nodes[record.TopologyKey];

            if (parentTopo is not null && !IsAllZero(parentTopo) && nodes.TryGetValue(parentKey, out UsbNode? parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                hostNode.Children.Insert(0, node);
            }
        }
    }

    private static UsbNode BuildDeviceRouterNode(RouterRecord record)
    {
        var usb4 = new Usb4Info
        {
            IsUsb4Capable = true,
            IsHostRouter = false,
        };
        ApplyFabricInfo(usb4, record);

        string label = usb4.AsciiModelName ?? "USB4 Device Router";
        if (usb4.AsciiVendorName is not null)
        {
            label = $"{usb4.AsciiVendorName} {label}".Trim();
        }

        return new UsbNode
        {
            Kind = UsbNodeKind.Usb4DeviceRouter,
            Name = $"USB4 Device Router: {label} (Topology {record.TopologyKey})",
            InstanceId = record.DeviceInstancePath,
            Usb4 = usb4,
        };
    }

    private static void ApplyFabric(UsbNode hostNode, RouterRecord record)
    {
        hostNode.Usb4 ??= new Usb4Info { IsUsb4Capable = true, IsHostRouter = true };
        ApplyFabricInfo(hostNode.Usb4, record);
    }

    private static void ApplyFabricInfo(Usb4Info usb4, RouterRecord record)
    {
        usb4.DomainId = record.DomainId != 0 ? record.DomainId : usb4.DomainId;
        usb4.TopologyId = record.TopologyKey;

        Usb4Event? info = record.Info;
        if (info is not null)
        {
            usb4.SiliconVendorId = ToUShort(info.Get("VendorId") ?? info.Get("SiliconVendorId"));
            usb4.SiliconProductId = ToUShort(info.Get("ProductId") ?? info.Get("SiliconProductId"));
            usb4.SiliconRevision = ToUInt(info.Get("SiliconRevision") ?? info.Get("Revision"));
            usb4.Usb4Version = info.Get("USB4Version") ?? info.Get("Usb4Version");
            usb4.Uuid = info.Get("UUID");
            usb4.AsciiVendorName = NullIfEmpty(info.Get("AsciiVendorName"));
            usb4.AsciiModelName = NullIfEmpty(info.Get("AsciiModelName"));
            usb4.UnitVendorId = ToUShort(info.Get("DeviceID") ?? info.Get("UnitVendorId"));
            usb4.UnitProductId = ToUShort(info.Get("ModelID") ?? info.Get("UnitProductId"));
            usb4.FirmwareVersion = info.Get("DeviceFirmwareVersion") ?? info.Get("FirmwareVersion");
        }

        usb4.DpInAdaptersTotal ??= record.DpInTotal;
        usb4.DpInAdaptersTunneled ??= record.DpInTunneled;
        usb4.DpInAdaptersUnavailable ??= record.DpInUnavailable;

        if (record.BandwidthGbps is double gbps)
        {
            usb4.CurrentBandwidthDownGbps = gbps;
            usb4.CurrentBandwidthUpGbps = gbps;
            usb4.LinkGeneration = record.Generation;
            usb4.LanesBonded = record.Lanes == 2;
        }
    }

    private static bool IsAllZero(byte[] topo) => topo.Length == 0 || topo.All(b => b == 0);

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static byte ToByte(string? s) =>
        TryParseFlexible(s, out long v) ? (byte)v : (byte)0;

    private static ushort? ToUShort(string? s) =>
        TryParseFlexible(s, out long v) ? (ushort)v : null;

    private static uint? ToUInt(string? s) =>
        TryParseFlexible(s, out long v) ? (uint)v : null;

    private static bool TryParseFlexible(string? s, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
