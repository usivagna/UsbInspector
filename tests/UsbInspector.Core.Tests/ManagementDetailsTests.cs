using UsbInspector.Core.Models;
using UsbInspector_App.ViewModels;

namespace UsbInspector.Core.Tests;

public class ManagementDetailsTests
{
    [Fact]
    public void ManagementAndTopologyViewsRemainWellFormedSiblingTabs()
    {
        var document = System.Xml.Linq.XDocument.Load(Path.Combine(AppContext.BaseDirectory, "MainPage.xaml"));
        System.Xml.Linq.XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var pivot = Assert.Single(document.Descendants(xaml + "Pivot"),
            element => (string?)element.Attribute("Grid.Row") == "2");
        Assert.Equal(["Device management", "Topology & advanced details"],
            pivot.Elements(xaml + "PivotItem").Select(element => (string?)element.Attribute("Header")));
    }

    [Theory]
    [InlineData(null, "Unknown")]
    [InlineData(true, "Read-only")]
    [InlineData(false, "Not read-only")]
    public void ReadOnlyStatusNeverGuesses(bool? readOnly, string expected)
    {
        var node = new UsbNode();
        node.StorageDevices.Add(new UsbStorageInfo { IsReadOnly = readOnly });
        DetailRow[] rows = DetailsBuilder.Storage(node).ToArray();
        Assert.Equal(expected, Assert.Single(rows, r => r.Label == "Read-only status").Value);
        Assert.Equal("Unknown", Assert.Single(rows, r => r.Label == "Disk capacity").Value);
        Assert.Equal("Unknown", Assert.Single(rows, r => r.Label == "Disk serial number").Value);
    }

    [Fact]
    public void EveryDiskAndVolumeIsShownSeparately()
    {
        var node = new UsbNode();
        var first = new UsbStorageInfo { DeviceId = "disk1", SerialNumber = "ONE", CapacityBytes = 1_073_741_824 };
        first.Volumes.Add(new UsbVolumeInfo { DriveLetter = "E:", FileSystem = "exFAT" });
        first.Volumes.Add(new UsbVolumeInfo { DriveLetter = "F:", FileSystem = "NTFS" });
        var second = new UsbStorageInfo { DeviceId = "disk2", SerialNumber = "TWO" };
        second.Volumes.Add(new UsbVolumeInfo());
        node.StorageDevices.AddRange([first, second]);
        DetailRow[] rows = DetailsBuilder.Storage(node).ToArray();
        Assert.Equal(["disk1", "disk2"], rows.Where(r => r.Label == "Disk").Select(r => r.Value));
        Assert.Equal(["E:", "F:", "Not assigned"], rows.Where(r => r.Label == "Drive letter").Select(r => r.Value));
        Assert.Equal(["exFAT", "NTFS", "Unknown"], rows.Where(r => r.Label == "File system").Select(r => r.Value));
        Assert.StartsWith("1 GiB", Assert.Single(rows, r => r.Label == "Disk capacity" && r.Value != "Unknown").Value);
    }

    [Fact]
    public void NonStorageAndMissingDescriptorsAreExplicit()
    {
        DetailRow[] rows = DetailsBuilder.Management(new UsbNode { Name = "Keyboard" }).ToArray();
        Assert.Equal("Unknown", Assert.Single(rows, r => r.Label == "USB serial number").Value);
        Assert.StartsWith("No disk reported", Assert.Single(rows, r => r.Label == "Storage").Value);
    }

    [Fact]
    public void PortCardUsesUserLabelAndStorageIdentities()
    {
        var node = new UsbNode { Kind = UsbNodeKind.Device, PortNumber = 3 };
        var disk = new UsbStorageInfo { SerialNumber = "CARD-A" };
        disk.Volumes.Add(new UsbVolumeInfo { DriveLetter = "G:" });
        node.StorageDevices.Add(disk);
        var entry = new PortMapEntry { Node = node, Route = "Controller / Port 3" };
        var item = new PortMapItem(entry, new(ChassisSide.Front, "Upper left"));
        Assert.Equal("Front · Upper left", item.LocationLabel);
        Assert.Equal("G: · CARD-A", item.Identity);
        Assert.Equal("Connected", item.Connection);
        Assert.Equal("Unassigned · Port 3", new PortMapItem(entry, null).LocationLabel);
    }
}
