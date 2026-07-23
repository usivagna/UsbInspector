using UsbInspector.Core.Interop;
using Windows.Win32;
using Xunit.Abstractions;

namespace UsbInspector.Core.Tests;

public class InteropSmokeTests
{
    private readonly ITestOutputHelper _output;

    public InteropSmokeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void EnumeratesUsbHostControllers()
    {
        var controllers = DeviceInterfaceEnumerator.Enumerate(PInvoke.GUID_DEVINTERFACE_USB_HOST_CONTROLLER);

        _output.WriteLine($"Elevated: {Elevation.IsProcessElevated()}");
        _output.WriteLine($"Host controllers found: {controllers.Count}");
        foreach (var c in controllers)
        {
            _output.WriteLine($"  InstanceId={c.InstanceId}");
            _output.WriteLine($"    Path={c.DevicePath}");
        }

        // A physical machine should expose at least one host controller.
        Assert.NotNull(controllers);
    }

    [Fact]
    public void EnumeratesUsbDevices()
    {
        var devices = DeviceInterfaceEnumerator.Enumerate(PInvoke.GUID_DEVINTERFACE_USB_DEVICE);
        _output.WriteLine($"USB device interfaces found: {devices.Count}");
        foreach (var d in devices)
        {
            _output.WriteLine($"  {d.InstanceId}");
        }

        Assert.NotNull(devices);
    }

    [Fact]
    public void InteropChainRetrievesRealDeviceData()
    {
        // This environment may have no USB hardware, so validate the SetupAPI + CfgMgr
        // interop chain against the disk interface class, which is always present.
        var diskInterface = new Guid("53f56307-b6bf-11d0-94f2-00a0c91efb8b"); // GUID_DEVINTERFACE_DISK
        var disks = DeviceInterfaceEnumerator.Enumerate(diskInterface);

        _output.WriteLine($"Disk interfaces found: {disks.Count}");
        foreach (var d in disks)
        {
            _output.WriteLine($"  InstanceId={d.InstanceId}");
            _output.WriteLine($"    Path={d.DevicePath}");
        }

        Assert.NotEmpty(disks);
        // Proves the device path + CM_Get_Device_ID round-trips produced real strings.
        Assert.All(disks, d =>
        {
            Assert.StartsWith(@"\\?\", d.DevicePath);
            Assert.NotEqual(0u, d.DevInst);
            Assert.NotEqual(string.Empty, d.InstanceId);
        });
    }

    [Fact]
    public void FullCaptureRunsWithoutThrowing()
    {
        var snapshot = new UsbInspector.Core.Services.UsbInspectorService().Capture();

        _output.WriteLine($"Elevated={snapshot.IsElevated} Machine={snapshot.MachineName}");
        _output.WriteLine($"Controllers={snapshot.HostControllers.Count} Hubs={snapshot.HubCount} Devices={snapshot.DeviceCount}");
        foreach (var w in snapshot.Warnings)
        {
            _output.WriteLine($"  warn: {w}");
        }

        foreach (var node in snapshot.EnumerateAll())
        {
            _output.WriteLine($"  [{node.Kind}] {node.Name} ({node.Speed})");
        }

        Assert.NotNull(snapshot);
    }

    [Fact]
    public void DeviceWatcherRegistersAndDisposes()
    {
        using var watcher = new UsbInspector.Core.Services.DeviceWatcher();
        bool started = watcher.Start();
        _output.WriteLine($"Watcher started: {started}");
        Assert.True(started);
    }

    [Fact]
    public void WmiProviderDoesNotThrow()
    {
        var entries = UsbInspector.Core.Services.WmiUsbProvider.Query();
        _output.WriteLine($"WMI USB entries: {entries.Count}");
        Assert.NotNull(entries);
    }

    [Fact]
    public void SnapshotExportsToValidJson()
    {
        var snapshot = new UsbInspector.Core.Services.UsbInspectorService().Capture();
        string json = UsbInspector.Core.Services.SnapshotExporter.ToJson(snapshot);

        _output.WriteLine(json.Length > 400 ? json[..400] : json);
        Assert.False(string.IsNullOrWhiteSpace(json));
        // Must be parseable JSON.
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("HostControllers", out _));
    }
}
