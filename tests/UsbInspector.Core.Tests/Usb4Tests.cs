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
    }
}
