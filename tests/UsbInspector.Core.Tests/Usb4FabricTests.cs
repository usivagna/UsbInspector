using UsbInspector.Core.Models;
using UsbInspector.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace UsbInspector.Core.Tests;

public class Usb4FabricTests
{
    private readonly ITestOutputHelper _output;

    public Usb4FabricTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void FormatTopologyIdJoinsSevenBytesWithColons()
    {
        Assert.Equal("0:0:0:0:0:0:0", Usb4Fabric.FormatTopologyId(new byte[7]));
        Assert.Equal("1:0:0:0:0:0:0", Usb4Fabric.FormatTopologyId(new byte[] { 1, 0, 0, 0, 0, 0, 0 }));
        Assert.Equal("", Usb4Fabric.FormatTopologyId(null));
        Assert.Equal("", Usb4Fabric.FormatTopologyId(Array.Empty<byte>()));
    }

    [Fact]
    public void ParentTopologyIdClearsDeepestNonZeroHop()
    {
        // Host router (all zero) has no parent.
        Assert.Null(Usb4Fabric.ParentTopologyId(new byte[7]));

        // A first-level device router's parent is the host router (all zero).
        byte[]? parent = Usb4Fabric.ParentTopologyId(new byte[] { 1, 0, 0, 0, 0, 0, 0 });
        Assert.NotNull(parent);
        Assert.All(parent!, b => Assert.Equal(0, b));

        // A second-level router nests under its first-level parent.
        byte[]? parent2 = Usb4Fabric.ParentTopologyId(new byte[] { 1, 3, 0, 0, 0, 0, 0 });
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0, 0 }, parent2);
    }

    [Fact]
    public void ComputeBandwidthMapsGen3DualLaneTo40Gbps()
    {
        // Gen 3 (bit 0x2), bonded => 40 Gbps.
        Usb4Fabric.LinkBandwidth? bw = Usb4Fabric.ComputeBandwidth(0x2, 0x2, laneBonded: true);
        Assert.NotNull(bw);
        Assert.Equal(40, bw!.Value.Gbps);
        Assert.Equal(3, bw.Value.Generation);
        Assert.Equal(2, bw.Value.Lanes);
        Assert.Contains("Gen 3", bw.Value.Label);
        Assert.Contains("dual", bw.Value.Label);
    }

    [Fact]
    public void ComputeBandwidthMapsGen2SingleLaneAndUnknown()
    {
        Usb4Fabric.LinkBandwidth? gen2 = Usb4Fabric.ComputeBandwidth(0x1, 0x1, laneBonded: false);
        Assert.NotNull(gen2);
        Assert.Equal(10, gen2!.Value.Gbps);
        Assert.Equal(2, gen2.Value.Generation);
        Assert.Equal(1, gen2.Value.Lanes);

        // No speed bits => not computable.
        Assert.Null(Usb4Fabric.ComputeBandwidth(0x0, 0x0, laneBonded: false));
    }

    [Fact]
    public void RundownReaderIsGracefulWhenNotElevated()
    {
        var warnings = new List<string>();
        Usb4RundownData data = new Usb4RundownReader().Read(warnings, TimeSpan.FromSeconds(1));

        // In a non-elevated test host this returns empty with an admin warning; never throws.
        if (data.RequiresElevation)
        {
            Assert.False(data.HasData);
            Assert.Contains(warnings, w => w.Contains("administrator", StringComparison.OrdinalIgnoreCase));
        }

        Assert.NotNull(data);
        _output.WriteLine($"RequiresElevation={data.RequiresElevation}, events={data.Events.Count}");
    }

    [Fact]
    public void FabricEnricherMapsEventsOntoHostRouterAndBuildsDeviceRouter()
    {
        var host = new UsbNode
        {
            Kind = UsbNodeKind.Usb4HostRouter,
            Name = "USB4 Host Router",
            InstanceId = "ACPI\\NVDA8100\\2",
            Usb4 = new Usb4Info { IsHostRouter = true, IsUsb4Capable = true, HostRouterInstanceId = "ACPI\\NVDA8100\\2" },
        };

        var data = new Usb4RundownData();

        var hostInfo = new Usb4Event
        {
            EventName = "DeviceRouterInformation",
            DomainId = 0x202,
            TopologyId = new byte[7],
            DeviceInstancePath = "ACPI\\NVDA8100\\2",
        };
        hostInfo.Fields["DeviceInstancePath"] = "ACPI\\NVDA8100\\2";
        hostInfo.Fields["VendorId"] = "0x0955";
        hostInfo.Fields["ProductId"] = "0x1372";
        hostInfo.Fields["USB4Version"] = "1.0";
        data.Events.Add(hostInfo);

        var devInfo = new Usb4Event
        {
            EventName = "DeviceRouterInformation",
            DomainId = 0x202,
            TopologyId = new byte[] { 1, 0, 0, 0, 0, 0, 0 },
            DeviceInstancePath = "USB4\\VID_8087&PID_0B26",
        };
        devInfo.Fields["AsciiVendorName"] = "Microsoft";
        devInfo.Fields["AsciiModelName"] = "Surface Thunderbolt(TM) 4 Dock";
        devInfo.Fields["DeviceFirmwareVersion"] = "40.1";
        data.Events.Add(devInfo);

        var port = new Usb4Event
        {
            EventName = "PortInformation",
            DomainId = 0x202,
            TopologyId = new byte[] { 1, 0, 0, 0, 0, 0, 0 },
        };
        port.Fields["IsDFP"] = "False";
        port.Fields["CurrentLinkSpeed"] = "2";
        port.Fields["NegotiatedLinkWidth"] = "2";
        port.Fields["LaneBonded"] = "True";
        data.Events.Add(port);

        new Usb4FabricEnricher().Enrich(new[] { host }, data, new List<string>());

        Assert.Equal((uint)0x202, host.Usb4!.DomainId);
        Assert.Equal("0:0:0:0:0:0:0", host.Usb4.TopologyId);
        Assert.Equal((ushort)0x0955, host.Usb4.SiliconVendorId);
        Assert.Equal("1.0", host.Usb4.Usb4Version);

        UsbNode? deviceRouter = host.Children.FirstOrDefault(c => c.Kind == UsbNodeKind.Usb4DeviceRouter);
        Assert.NotNull(deviceRouter);
        Assert.Equal("Microsoft", deviceRouter!.Usb4!.AsciiVendorName);
        Assert.Equal("Surface Thunderbolt(TM) 4 Dock", deviceRouter.Usb4.AsciiModelName);
        Assert.Equal("40.1", deviceRouter.Usb4.FirmwareVersion);
        Assert.Equal(40, deviceRouter.Usb4.CurrentBandwidthDownGbps);
        Assert.Equal(3, deviceRouter.Usb4.LinkGeneration);
        Assert.True(deviceRouter.Usb4.LanesBonded);
    }

    [Fact]
    public void FabricEnricherFlagsElevationRequirement()
    {
        var host = new UsbNode
        {
            Kind = UsbNodeKind.Usb4HostRouter,
            Name = "USB4 Host Router",
            Usb4 = new Usb4Info { IsHostRouter = true },
        };
        var data = new Usb4RundownData { RequiresElevation = true };

        new Usb4FabricEnricher().Enrich(new[] { host }, data, new List<string>());

        Assert.True(host.Usb4!.FabricDetailRequiresElevation);
        Assert.Empty(host.Children);
    }
}

public class PhysicalPortTests
{
    [Fact]
    public void ConnectorPrefixDropsFinalSegment()
    {
        string? key = PortLocation.ConnectorPrefix(new[] { "PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(1)#USB(2)" });
        Assert.Equal("PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(1)", key);

        Assert.Null(PortLocation.ConnectorPrefix(null));
        Assert.Null(PortLocation.ConnectorPrefix(new[] { "" }));
    }

    [Fact]
    public void AcpiConnectorTokenExtractsAcpiSegment()
    {
        string? token = PortLocation.AcpiConnectorToken(new[] { "ACPI(_SB_)#ACPI(PCI0)#PCI(1400)" });
        Assert.Equal("ACPI:_SB_", token);
        Assert.Null(PortLocation.AcpiConnectorToken(new[] { "PCIROOT(0)#PCI(1400)" }));
    }

    [Fact]
    public void ComputePortKeyPrefersUsb4FabricPort()
    {
        var node = new UsbNode
        {
            Kind = UsbNodeKind.Usb4DeviceRouter,
            Usb4 = new Usb4Info { DomainId = 0x202, TopologyId = "1:0:0:0:0:0:0" },
            Pnp = new PnpDeviceProperties { LocationPaths = new[] { "ACPI(_SB_)#PCI(1400)" } },
        };

        var computed = PhysicalPortGrouper.ComputePortKey(node);
        Assert.NotNull(computed);
        Assert.Equal(PortGroupSource.Usb4FabricPort, computed!.Value.Source);
        Assert.Equal("USB4:202:1:0:0:0:0:0:0", computed.Value.Key);
    }

    [Fact]
    public void BuildPhysicalPortGroupsClustersSharedConnector()
    {
        var snapshot = new UsbSnapshot();

        // Two functions sharing the same location-path prefix (same connector).
        var controller = new UsbNode { Kind = UsbNodeKind.HostController, Name = "xHCI", InstanceId = "X" };
        controller.Children.Add(new UsbNode
        {
            Kind = UsbNodeKind.Device,
            Name = "USB3 function",
            InstanceId = "A",
            Pnp = new PnpDeviceProperties { LocationPaths = new[] { "PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(1)" } },
        });
        snapshot.HostControllers.Add(controller);

        var router = new UsbNode
        {
            Kind = UsbNodeKind.Usb4HostRouter,
            Name = "USB4 function",
            InstanceId = "B",
            Pnp = new PnpDeviceProperties { LocationPaths = new[] { "PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(2)" } },
        };
        snapshot.Usb4HostRouters.Add(router);

        IReadOnlyList<PhysicalPortGroup> groups = snapshot.BuildPhysicalPortGroups();

        Assert.Single(groups);
        Assert.Equal(2, groups[0].Members.Count);
        Assert.Equal(PortGroupSource.LocationPathPrefix, groups[0].Source);
    }

    [Fact]
    public void ExportIncludesPhysicalPorts()
    {
        var snapshot = new UsbSnapshot();
        string json = SnapshotExporter.ToJson(snapshot);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("PhysicalPorts", out _));
    }
}
