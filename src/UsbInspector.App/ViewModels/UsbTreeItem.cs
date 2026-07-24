using System.Collections.ObjectModel;
using UsbInspector.Core.Models;
using UsbInspector.Core.Services;

namespace UsbInspector_App.ViewModels;

/// <summary>A tree-view wrapper around a <see cref="UsbNode"/> for the topology pane.</summary>
public sealed class UsbTreeItem
{
    public UsbTreeItem(UsbNode node)
    {
        Node = node;
        Children = new ObservableCollection<UsbTreeItem>(node.Children.Select(c => new UsbTreeItem(c)));
    }

    /// <summary>
    /// Wraps a node with an explicit set of child items. Used by the "Physical devices" grouped view
    /// to reference real nodes as flat leaves (so selection shows full details) without expanding
    /// their own topology subtree.
    /// </summary>
    public UsbTreeItem(UsbNode node, IEnumerable<UsbTreeItem> children)
    {
        Node = node;
        Children = new ObservableCollection<UsbTreeItem>(children);
    }

    public UsbNode Node { get; }

    public ObservableCollection<UsbTreeItem> Children { get; }

    public string Name => Node.Name;

    public bool HasWarnings => Node.Warnings.Count > 0;

    /// <summary>Segoe MDL2 glyph representing the node kind (device-type aware for real devices).</summary>
    public string Glyph => Node.Kind switch
    {
        UsbNodeKind.HostController => "\uE950", // chip
        UsbNodeKind.RootHub => "\uE8CE",        // hub-ish
        UsbNodeKind.ExternalHub => "\uE8CE",
        UsbNodeKind.Device => DeviceGlyph(),
        UsbNodeKind.PnpDevice => DeviceGlyph(),
        UsbNodeKind.EmptyPort => "\uE7B3",      // empty slot
        UsbNodeKind.Usb4HostRouter => "\uE945", // lightning / high-speed link
        UsbNodeKind.Usb4DeviceRouter => "\uE945",
        UsbNodeKind.PhysicalGroupRoot => "\uE7F4", // collection
        UsbNodeKind.PhysicalGroup => "\uE977",  // package / enclosure
        UsbNodeKind.PhysicalPortRoot => "\uE7F4",
        UsbNodeKind.PhysicalPort => "\uE7F8",   // connector / port
        _ => "\uE7F4",
    };

    /// <summary>A friendly device-type label (e.g. "Keyboard", "Storage drive").</summary>
    public string FriendlyType => UsbDeviceClassifier.FriendlyType(UsbDeviceClassifier.Classify(Node));

    private string DeviceGlyph() => UsbDeviceClassifier.Classify(Node) switch
    {
        UsbDeviceCategory.Keyboard => "\uE765",
        UsbDeviceCategory.Mouse => "\uE962",
        UsbDeviceCategory.InputDevice => "\uE7FC",
        UsbDeviceCategory.Storage => "\uEDA2",
        UsbDeviceCategory.Camera => "\uE722",
        UsbDeviceCategory.Audio => "\uE767",
        UsbDeviceCategory.Printer => "\uE749",
        UsbDeviceCategory.Scanner => "\uE8FE",
        UsbDeviceCategory.Network => "\uE839",
        UsbDeviceCategory.Phone => "\uE8EA",
        UsbDeviceCategory.SmartCard => "\uE8D7",
        UsbDeviceCategory.Wireless => "\uE704",
        UsbDeviceCategory.Display => "\uE7F4",
        UsbDeviceCategory.Hub => "\uE8CE",
        _ => "\uE88E", // generic device
    };
}
