using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;
using UsbInspector.Core.Services;
using Xunit.Abstractions;

namespace UsbInspector.Core.Tests;

public class Usb4Tests
{
    private readonly ITestOutputHelper _output;

    public Usb4Tests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Usb4InfoHasAnyDataReflectsPopulatedFields()
    {
        var empty = new Usb4Info();
        Assert.False(empty.HasAnyData);

        Assert.True(new Usb4Info { IsHostRouter = true }.HasAnyData);
        Assert.True(new Usb4Info { IsUsb4Capable = true }.HasAnyData);
        Assert.True(new Usb4Info { LinkSpeedLabel = "USB4 (40 Gbps)" }.HasAnyData);
        Assert.True(new Usb4Info { SupportsTunnelledUsb = true }.HasAnyData);

        var withCap = new Usb4Info();
        withCap.Capabilities.Add("something");
        Assert.True(withCap.HasAnyData);
    }

    [Fact]
    public void Usb4SpeedTiersAreDistinctAndOrdered()
    {
        Assert.Equal(6, (int)UsbSpeed.Usb4Gen2x2);
        Assert.Equal(7, (int)UsbSpeed.Usb4Gen3x2);
        Assert.True(UsbSpeed.Usb4Gen3x2 > UsbSpeed.SuperPlus20);
    }

    [Fact]
    public void EnumerateAllPresentDevnodesReturnsData()
    {
        var devices = DeviceInfoEnumerator.EnumerateAllPresent();
        _output.WriteLine($"Present devnodes: {devices.Count}");

        Assert.NotNull(devices);
        // A running machine always has present devnodes.
        Assert.NotEmpty(devices);
        Assert.All(devices, d => Assert.NotEqual(0u, d.DevInst));
    }

    [Fact]
    public void Usb4EnumeratorDoesNotThrow()
    {
        var warnings = new List<string>();
        IReadOnlyList<UsbNode> routers = new Usb4Enumerator().Enumerate(warnings);

        _output.WriteLine($"USB4 host routers found: {routers.Count}");
        foreach (UsbNode r in routers)
        {
            _output.WriteLine($"  [{r.Kind}] {r.Name}");
            Assert.Equal(UsbNodeKind.Usb4HostRouter, r.Kind);
            Assert.NotNull(r.Usb4);
            Assert.True(r.Usb4!.IsHostRouter);
        }

        Assert.NotNull(routers);
    }

    [Fact]
    public void CaptureExposesUsb4HostRouterSectionAndExportsIt()
    {
        UsbSnapshot snapshot = new UsbInspectorService().Capture();
        _output.WriteLine($"USB4 routers: {snapshot.Usb4RouterCount}");

        Assert.NotNull(snapshot.Usb4HostRouters);
        Assert.Equal(snapshot.Usb4HostRouters.Count, snapshot.Usb4RouterCount);

        string json = SnapshotExporter.ToJson(snapshot);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("Usb4HostRouters", out _));
        Assert.True(doc.RootElement.TryGetProperty("PhysicalDevices", out _));
    }

    [Fact]
    public void Usb4HostRouterChildrenAreEnumeratedWhenPresent()
    {
        var warnings = new List<string>();
        IReadOnlyList<UsbNode> routers = new Usb4Enumerator().Enumerate(warnings);

        foreach (UsbNode router in routers)
        {
            _output.WriteLine($"{router.Name}: {router.Children.Count} child devnode(s)");
            // Every enumerated child must be a PnP devnode carrying properties.
            foreach (UsbNode child in router.Children)
            {
                Assert.Equal(UsbNodeKind.PnpDevice, child.Kind);
                Assert.NotNull(child.Pnp);
            }
        }

        Assert.NotNull(routers);
    }

    [Fact]
    public void IsRealContainerIdRejectsEmptyAndZeroGuid()
    {
        Assert.False(UsbSnapshot.IsRealContainerId(null));
        Assert.False(UsbSnapshot.IsRealContainerId(""));
        Assert.False(UsbSnapshot.IsRealContainerId("   "));
        Assert.False(UsbSnapshot.IsRealContainerId("{00000000-0000-0000-0000-000000000000}"));
        Assert.True(UsbSnapshot.IsRealContainerId("{11111111-1111-1111-1111-111111111111}"));
    }

    [Fact]
    public void BuildPhysicalGroupsClustersByContainerIdAndExcludesNoise()
    {
        const string shared = "{11111111-1111-1111-1111-111111111111}";
        const string lone = "{22222222-2222-2222-2222-222222222222}";
        const string zero = "{00000000-0000-0000-0000-000000000000}";

        var snapshot = new UsbSnapshot();
        var controller = new UsbNode
        {
            Kind = UsbNodeKind.HostController,
            Name = "Controller",
            InstanceId = "A",
            Pnp = new PnpDeviceProperties { ContainerId = shared },
        };
        controller.Children.Add(new UsbNode
        {
            Kind = UsbNodeKind.Device,
            Name = "Dock function",
            InstanceId = "B",
            Pnp = new PnpDeviceProperties { ContainerId = shared },
        });
        snapshot.HostControllers.Add(controller);
        snapshot.HostControllers.Add(new UsbNode
        {
            Kind = UsbNodeKind.Device,
            Name = "Lone device",
            InstanceId = "C",
            Pnp = new PnpDeviceProperties { ContainerId = lone },
        });
        snapshot.HostControllers.Add(new UsbNode
        {
            Kind = UsbNodeKind.Device,
            Name = "No container",
            InstanceId = "D",
            Pnp = new PnpDeviceProperties { ContainerId = zero },
        });

        IReadOnlyList<PhysicalDeviceGroup> groups = snapshot.BuildPhysicalGroups();

        // Only the shared container (2 members) is a meaningful group; lone/zero are excluded.
        Assert.Single(groups);
        Assert.Equal(shared, groups[0].ContainerId);
        Assert.Equal(2, groups[0].Members.Count);
        Assert.Equal("Dock function", groups[0].Name);
    }
}
