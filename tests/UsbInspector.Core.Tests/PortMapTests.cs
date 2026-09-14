using UsbInspector.Core.Models;
using UsbInspector.Core.Services;

namespace UsbInspector.Core.Tests;

public class PortMapTests
{
    [Fact]
    public void PortIdentitySurvivesUnplugAndDeviceReplacement()
    {
        var (snapshot, root) = CreateTopology();
        var node = new UsbNode { Kind = UsbNodeKind.Device, PortNumber = 2, InstanceId = "USB\\A" };
        root.Children.Add(node);
        string? key = Assert.Single(PortMapBuilder.Build(snapshot)).Key;
        node.Kind = UsbNodeKind.EmptyPort;
        node.InstanceId = null;
        Assert.Equal(key, Assert.Single(PortMapBuilder.Build(snapshot)).Key);
        node.Kind = UsbNodeKind.Device;
        node.InstanceId = "USB\\B";
        Assert.Equal(key, Assert.Single(PortMapBuilder.Build(snapshot)).Key);
        Assert.NotNull(key);
    }

    [Fact]
    public void FullRouteAndControllerDistinguishIdenticalPortNumbers()
    {
        var (snapshot, root) = CreateTopology();
        var hub = new UsbNode { Kind = UsbNodeKind.ExternalHub, PortNumber = 1, InstanceId = "USB\\HUB" };
        root.Children.Add(hub);
        hub.Children.Add(new UsbNode { Kind = UsbNodeKind.Device, PortNumber = 1 });
        var otherController = new UsbNode { Kind = UsbNodeKind.HostController, InstanceId = "PCI\\OTHER" };
        var otherRoot = new UsbNode { Kind = UsbNodeKind.RootHub };
        otherRoot.Children.Add(new UsbNode { Kind = UsbNodeKind.EmptyPort, PortNumber = 1 });
        otherController.Children.Add(otherRoot);
        snapshot.HostControllers.Add(otherController);
        var ports = PortMapBuilder.Build(snapshot);
        Assert.Equal(3, ports.Select(p => p.Key).Distinct().Count());
        Assert.Equal(2, ports.Count(p => p.IsConnected));
        Assert.Contains("/ Port 1 /", ports[1].Route);
    }

    [Fact]
    public void ReplacingHubDoesNotReuseDownstreamLabels()
    {
        var (snapshot, root) = CreateTopology();
        var hub = new UsbNode { Kind = UsbNodeKind.ExternalHub, PortNumber = 1, InstanceId = "USB\\HUB-A" };
        root.Children.Add(hub);
        hub.Children.Add(new UsbNode { Kind = UsbNodeKind.Device, PortNumber = 1 });
        var before = PortMapBuilder.Build(snapshot);
        hub.InstanceId = "USB\\HUB-B";
        var after = PortMapBuilder.Build(snapshot);
        Assert.Equal(before[0].Key, after[0].Key);
        Assert.NotEqual(before[1].Key, after[1].Key);
        hub.InstanceId = null;
        Assert.Null(PortMapBuilder.Build(snapshot)[1].Key);
    }

    [Fact]
    public void UnknownControllerCannotReceivePersistentLabels()
    {
        var (snapshot, root) = CreateTopology();
        snapshot.HostControllers[0].InstanceId = null;
        root.Children.Add(new UsbNode { Kind = UsbNodeKind.EmptyPort, PortNumber = 1 });
        Assert.Null(Assert.Single(PortMapBuilder.Build(snapshot)).Key);
    }

    [Fact]
    public void FailedEnumerationIsNotReportedAsConnected()
    {
        var (snapshot, root) = CreateTopology();
        root.Children.Add(new UsbNode
        {
            Kind = UsbNodeKind.EmptyPort,
            PortNumber = 1,
            Power = new PortPowerInfo { ConnectionStatus = UsbPortConnectionStatus.DeviceFailedEnumeration },
        });
        var entry = Assert.Single(PortMapBuilder.Build(snapshot));
        Assert.False(entry.IsConnected);
        Assert.Equal(UsbPortConnectionStatus.DeviceFailedEnumeration, entry.Node.Power!.ConnectionStatus);
    }

    [Fact]
    public void LayoutRoundTripsAndCanClearAssignment()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var store = new PortLayoutStore(Path.Combine(directory, "layout.json"));
            Assert.Empty(store.Load());
            var assignments = new Dictionary<string, PortAssignment>
            {
                ["controller/port:1"] = new(ChassisSide.Front, "Left socket"),
                ["controller/port:2"] = new(ChassisSide.Rear, "Backup"),
            };
            store.Save(assignments);
            Assert.Equal(assignments["controller/port:1"], store.Load()["CONTROLLER/PORT:1"]);
            assignments.Clear();
            store.Save(assignments);
            Assert.Empty(store.Load());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"port\":null}")]
    [InlineData("{\"port\":{\"Location\":99,\"Label\":\"Bad\"}}")]
    public void InvalidLayoutIsNotSilentlyAccepted(string json)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            Assert.ThrowsAny<Exception>(() => new PortLayoutStore(path).Load());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (UsbSnapshot Snapshot, UsbNode Root) CreateTopology()
    {
        var snapshot = new UsbSnapshot();
        var controller = new UsbNode { Kind = UsbNodeKind.HostController, InstanceId = "PCI\\HOST", Name = "xHCI" };
        var root = new UsbNode { Kind = UsbNodeKind.RootHub };
        controller.Children.Add(root);
        snapshot.HostControllers.Add(controller);
        return (snapshot, root);
    }
}
