using UsbInspector.Core.Models;
using UsbInspector.Core.Services;
using Xunit;

namespace UsbInspector.Core.Tests;

/// <summary>
/// Tests for the pure logic added by the usability overhaul: device-type classification,
/// physical-port confidence badges, and the plain-text snapshot report builder.
/// </summary>
public class UsabilityTests
{
    private static UsbNode DeviceWithDeviceClass(byte cls, byte sub = 0, byte proto = 0, string name = "Test device") =>
        new()
        {
            Kind = UsbNodeKind.Device,
            Name = name,
            DeviceDescriptor = new UsbDeviceDescriptorInfo
            {
                DeviceClass = cls,
                DeviceSubClass = sub,
                DeviceProtocol = proto,
            },
        };

    private static UsbNode CompositeWithInterface(byte cls, byte sub = 0, byte proto = 0)
    {
        var node = new UsbNode
        {
            Kind = UsbNodeKind.Device,
            Name = "Composite device",
            DeviceDescriptor = new UsbDeviceDescriptorInfo { DeviceClass = 0x00 },
        };
        var config = new UsbConfigurationDescriptorInfo();
        config.Interfaces.Add(new UsbInterfaceDescriptorInfo
        {
            InterfaceClass = cls,
            InterfaceSubClass = sub,
            InterfaceProtocol = proto,
        });
        node.Configurations.Add(config);
        return node;
    }

    [Theory]
    [InlineData(0x03, 0x01, UsbDeviceCategory.Keyboard)]
    [InlineData(0x03, 0x02, UsbDeviceCategory.Mouse)]
    [InlineData(0x03, 0x00, UsbDeviceCategory.InputDevice)]
    [InlineData(0x08, 0x00, UsbDeviceCategory.Storage)]
    [InlineData(0x0E, 0x00, UsbDeviceCategory.Camera)]
    [InlineData(0x01, 0x00, UsbDeviceCategory.Audio)]
    [InlineData(0x07, 0x00, UsbDeviceCategory.Printer)]
    [InlineData(0x09, 0x00, UsbDeviceCategory.Hub)]
    [InlineData(0xE0, 0x00, UsbDeviceCategory.Wireless)]
    public void ClassifyMapsDeviceClassProtocolToCategory(byte cls, byte proto, UsbDeviceCategory expected)
    {
        UsbNode node = DeviceWithDeviceClass(cls, proto: proto);
        Assert.Equal(expected, UsbDeviceClassifier.Classify(node));
    }

    [Fact]
    public void ClassifyUsesNodeKindForControllersHubsAndRouters()
    {
        Assert.Equal(UsbDeviceCategory.HostController,
            UsbDeviceClassifier.Classify(new UsbNode { Kind = UsbNodeKind.HostController }));
        Assert.Equal(UsbDeviceCategory.Hub,
            UsbDeviceClassifier.Classify(new UsbNode { Kind = UsbNodeKind.RootHub }));
        Assert.Equal(UsbDeviceCategory.Usb4Router,
            UsbDeviceClassifier.Classify(new UsbNode { Kind = UsbNodeKind.Usb4HostRouter }));
    }

    [Fact]
    public void ClassifyFallsBackToInterfaceClassForComposite()
    {
        Assert.Equal(UsbDeviceCategory.Storage, UsbDeviceClassifier.Classify(CompositeWithInterface(0x08)));
        Assert.Equal(UsbDeviceCategory.Keyboard, UsbDeviceClassifier.Classify(CompositeWithInterface(0x03, proto: 0x01)));
    }

    [Fact]
    public void ClassifyUnknownIsGeneric()
    {
        Assert.Equal(UsbDeviceCategory.Generic, UsbDeviceClassifier.Classify(DeviceWithDeviceClass(0xFF)));
    }

    [Fact]
    public void FriendlyTypeReturnsPlainLabels()
    {
        Assert.Equal("Keyboard", UsbDeviceClassifier.FriendlyType(UsbDeviceCategory.Keyboard));
        Assert.Equal("Storage drive", UsbDeviceClassifier.FriendlyType(UsbDeviceCategory.Storage));
        Assert.Equal("USB device", UsbDeviceClassifier.FriendlyType(UsbDeviceCategory.Generic));
    }

    [Theory]
    [InlineData(PortGroupSource.Usb4FabricPort, true, "✓ Confirmed")]
    [InlineData(PortGroupSource.AcpiPld, true, "✓ Confirmed")]
    [InlineData(PortGroupSource.LocationPathPrefix, false, "~ Likely")]
    [InlineData(PortGroupSource.LocationInfo, false, "~ Likely")]
    public void ConfidenceBadgeReflectsSource(PortGroupSource source, bool highConfidence, string badge)
    {
        var group = new PhysicalPortGroup { Source = source };
        Assert.Equal(highConfidence, group.IsHighConfidence);
        Assert.Equal(badge, group.ConfidenceBadge);
    }

    [Fact]
    public void SnapshotReportIncludesDeviceCountAndFriendlyType()
    {
        var snapshot = new UsbSnapshot { IsElevated = true };
        var controller = new UsbNode { Kind = UsbNodeKind.HostController, Name = "xHCI controller" };
        controller.Children.Add(DeviceWithDeviceClass(0x08, name: "Flash drive"));
        snapshot.HostControllers.Add(controller);

        string report = SnapshotReport.Build(snapshot);

        Assert.Contains("USB Inspector", report);
        Assert.Contains("Flash drive", report);
        Assert.Contains("Storage drive", report);
        Assert.Contains("1 device(s) connected", report);
    }

    [Fact]
    public void SnapshotReportDoesNotThrowOnEmptySnapshot()
    {
        string report = SnapshotReport.Build(new UsbSnapshot());
        Assert.Contains("0 device(s) connected", report);
    }
}
