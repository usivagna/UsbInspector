using System.Text.Json;
using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;
using UsbInspector.Core.Services;

namespace UsbInspector.Core.Tests;

public class StorageInfoTests
{
    private const string DiskId = @"SCSI\DISK&VEN_TEST&PROD_STORAGE\4&123&0&000000";
    private const string UsbId = @"USB\VID_1234&PID_5678\DEVICE";
    private const string HubId = @"USB\VID_1234&PID_9999\HUB";

    private static UsbNode Device(string? id) => new() { Kind = UsbNodeKind.Device, InstanceId = id };

    [Theory]
    [InlineData(DiskId)]
    [InlineData(@"USBSTOR\DISK&VEN_TEST&PROD_STORAGE\DEVICE&0")]
    public void MatchesBothUaspAndUsbStorThroughExactAncestry(string diskId)
    {
        var usb = Device(UsbId);
        var unrelated = Device(@"USB\VID_1234&PID_5678\OTHER");
        Assert.Same(usb, StorageInfoEnricher.FindNearestUsbNode(
            diskId, new[] { diskId, @"USB\VID_1234&PID_5678&MI_00\FUNCTION", UsbId, HubId },
            new[] { unrelated, usb }));
    }

    [Fact]
    public void ChoosesNearestNodeRegardlessOfTreeEnumerationOrder()
    {
        var function = Device(@"USB\VID_1234&PID_5678&MI_00\FUNCTION");
        var parent = Device(UsbId);
        var result = StorageInfoEnricher.FindNearestUsbNode(
            DiskId, new[] { DiskId, function.InstanceId!, UsbId }, new[] { parent, function });
        Assert.Same(function, result);
        Assert.Empty(parent.StorageDevices);
    }

    [Fact]
    public void MatchesExactInstanceIdsCaseInsensitively()
    {
        var usb = Device(UsbId.ToLowerInvariant());
        Assert.Same(usb, StorageInfoEnricher.FindNearestUsbNode(
            DiskId.ToLowerInvariant(), new[] { DiskId, UsbId }, new[] { usb }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AbsentDiskIdentityNeverMatches(string? diskId)
    {
        Assert.Null(StorageInfoEnricher.FindNearestUsbNode(
            diskId, new[] { DiskId, UsbId }, new[] { Device(UsbId), Device(null) }));
    }

    [Fact]
    public void EmptyOrUnrelatedAncestryNeverMatches()
    {
        var nodes = new[] { Device(UsbId) };
        Assert.Null(StorageInfoEnricher.FindNearestUsbNode(DiskId, Array.Empty<string>(), nodes));
        Assert.Null(StorageInfoEnricher.FindNearestUsbNode(DiskId, new[] { UsbId }, nodes));
        Assert.Null(StorageInfoEnricher.FindNearestUsbNode(DiskId, new[] { DiskId, "", UsbId }, nodes));
        Assert.Null(StorageInfoEnricher.FindNearestUsbNode(DiskId, new[] { DiskId, DiskId, UsbId }, nodes));
    }

    [Fact]
    public void SharedNamesAndPartialIdentitiesAreNotEvidence()
    {
        var usb = Device(UsbId + "-OTHER");
        usb.Name = "Test storage";
        usb.StorageDevices.Add(new UsbStorageInfo { Model = "Test storage", SerialNumber = "DEVICE" });
        Assert.Null(StorageInfoEnricher.FindNearestUsbNode(
            DiskId, new[] { DiskId, UsbId }, new[] { usb, Device(null) }));
    }

    [Fact]
    public void AmbiguousNearestIdentityDoesNotFallBackToParent()
    {
        var nodes = new[] { Device(DiskId), Device(DiskId.ToLowerInvariant()), Device(UsbId) };
        Assert.Null(StorageInfoEnricher.FindNearestUsbNode(DiskId, new[] { DiskId, UsbId }, nodes));
    }

    [Fact]
    public void Usb4PnpAliasesDoNotBlockOrMakeUsbDevicesAmbiguous()
    {
        var snapshot = new UsbSnapshot();
        var usb = Device(UsbId);
        snapshot.HostControllers.Add(usb);
        var router = new UsbNode { Kind = UsbNodeKind.Usb4HostRouter, InstanceId = @"PCI\ROUTER" };
        router.Children.Add(new UsbNode { Kind = UsbNodeKind.PnpDevice, InstanceId = DiskId });
        router.Children.Add(new UsbNode { Kind = UsbNodeKind.PnpDevice, InstanceId = UsbId });
        snapshot.Usb4HostRouters.Add(router);

        Assert.Same(usb, StorageInfoEnricher.FindNearestUsbNode(
            DiskId, new[] { DiskId, UsbId, router.InstanceId! }, snapshot.EnumerateAll()));
        Assert.All(router.Children, node => Assert.Empty(node.StorageDevices));
    }

    [Fact]
    public void PnpAliasAloneIsNotAStorageTarget()
    {
        Assert.Null(StorageInfoEnricher.FindNearestUsbNode(DiskId, new[] { DiskId, UsbId },
            new[] { new UsbNode { Kind = UsbNodeKind.PnpDevice, InstanceId = UsbId } }));
    }

    [Fact]
    public void ReadOnlyQueryUsesExactPnpInterfaceNotReusedDiskNumber()
    {
        var interfaces = new[]
        {
            new DeviceInterfaceInstance(@"\\?\disk#replacement", 2, DiskId + "-REPLACEMENT"),
            new DeviceInterfaceInstance(@"\\?\disk#original", 1, DiskId.ToLowerInvariant()),
        };
        Assert.Equal(@"\\?\disk#original", StorageInfoEnricher.FindDiskInterfacePath(DiskId, interfaces));
        Assert.Null(StorageInfoEnricher.FindDiskInterfacePath(DiskId, interfaces.Take(1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingIdentityCannotDetermineReadOnlyStatus(string? identity)
    {
        Assert.Null(StorageInfoEnricher.FindDiskInterfacePath(identity,
            new[] { new DeviceInterfaceInstance(@"\\?\disk#original", 1, DiskId) }));
    }

    [Fact]
    public void MissingOrAmbiguousDiskInterfacesRemainUnknown()
    {
        Assert.Null(StorageInfoEnricher.FindDiskInterfacePath(DiskId, Array.Empty<DeviceInterfaceInstance>()));
        Assert.Null(StorageInfoEnricher.FindDiskInterfacePath(DiskId,
            new[] { new DeviceInterfaceInstance("", 1, DiskId) }));
        Assert.Null(StorageInfoEnricher.FindDiskInterfacePath(DiskId, new[]
        {
            new DeviceInterfaceInstance(@"\\?\disk#one", 1, DiskId),
            new DeviceInterfaceInstance(@"\\?\disk#two", 2, DiskId),
        }));
    }

    [Theory]
    [InlineData(UsbNodeKind.RootHub, false)]
    [InlineData(UsbNodeKind.ExternalHub, false)]
    [InlineData(UsbNodeKind.HostController, false)]
    [InlineData(UsbNodeKind.Usb4HostRouter, false)]
    [InlineData(UsbNodeKind.Device, true)]
    public void InfrastructureIsABarrierNotAnAggregateTarget(UsbNodeKind kind, bool isHub)
    {
        var hub = new UsbNode { Kind = kind, IsHub = isHub, InstanceId = HubId };
        var parent = Device(UsbId);
        Assert.Null(StorageInfoEnricher.FindNearestUsbNode(
            DiskId, new[] { DiskId, HubId, UsbId }, new[] { parent, hub }));
        Assert.Empty(hub.StorageDevices);
        Assert.Empty(parent.StorageDevices);
    }

    [Fact]
    public void MultipleDisksAndVolumesRemainOnTheMatchingDeviceAndSerialize()
    {
        var snapshot = new UsbSnapshot { ComputerManufacturer = "PC maker", ComputerModel = "PC model" };
        var hub = new UsbNode { Kind = UsbNodeKind.ExternalHub, InstanceId = HubId, IsHub = true };
        var usb = Device(UsbId);
        hub.Children.Add(usb);
        snapshot.HostControllers.Add(hub);

        for (int i = 0; i < 2; i++)
        {
            string id = DiskId + i;
            var disk = new UsbStorageInfo { DeviceId = @"\\.\PHYSICALDRIVE" + i, CapacityBytes = 4096 };
            disk.Volumes.Add(new UsbVolumeInfo { DriveLetter = i == 0 ? "E:" : "G:", CapacityBytes = 1024 });
            disk.Volumes.Add(new UsbVolumeInfo { DriveLetter = i == 0 ? "F:" : null, Label = "Data" });
            UsbNode? owner = StorageInfoEnricher.FindNearestUsbNode(
                id, new[] { id, UsbId, HubId }, snapshot.EnumerateAll());
            Assert.Same(usb, owner);
            owner!.StorageDevices.Add(disk);
        }

        Assert.Empty(hub.StorageDevices);
        Assert.Equal(2, usb.StorageDevices.Count);
        Assert.All(usb.StorageDevices, disk => Assert.Equal(2, disk.Volumes.Count));
        Assert.Null(usb.StorageDevices[1].Volumes[1].DriveLetter);
        Assert.Null(usb.StorageDevices[1].Volumes[1].CapacityBytes);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot));
        Assert.Equal("PC maker", json.RootElement.GetProperty("ComputerManufacturer").GetString());
        Assert.Equal("PC model", json.RootElement.GetProperty("ComputerModel").GetString());
        var disks = json.RootElement.GetProperty("HostControllers")[0].GetProperty("Children")[0]
            .GetProperty("StorageDevices");
        Assert.Equal(2, disks.GetArrayLength());
        Assert.Equal(2, disks[0].GetProperty("Volumes").GetArrayLength());
    }

    [Fact]
    public void MissingMetadataAndUnmountedDisksStayUnknown()
    {
        var disk = new UsbStorageInfo();
        Assert.Empty(disk.Volumes);
        Assert.Null(disk.CapacityBytes);
        Assert.Null(disk.IsReadOnly);
        Assert.Null(disk.DeviceId);
        Assert.Null(new UsbSnapshot().ComputerModel);
        Assert.Null(new UsbSnapshot().ComputerManufacturer);
        Assert.Empty(Device(UsbId).StorageDevices);
    }

    [Theory]
    [InlineData(true, 0, false)]
    [InlineData(true, 19, false)]
    [InlineData(false, 19, true)]
    [InlineData(false, 0, null)]
    [InlineData(false, 1, null)]
    [InlineData(false, 5, null)]
    [InlineData(false, 21, null)]
    [InlineData(false, 50, null)]
    public void ReadOnlyRequiresExplicitEvidence(bool succeeded, int errorCode, bool? expected)
    {
        Assert.Equal(expected, StorageInfoEnricher.InterpretWriteProtection(succeeded, errorCode));
    }
}
