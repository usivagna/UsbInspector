using System.Collections.ObjectModel;
using UsbInspector.Core.Models;

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

    /// <summary>Segoe MDL2 glyph representing the node kind.</summary>
    public string Glyph => Node.Kind switch
    {
        UsbNodeKind.HostController => "\uE950", // chip
        UsbNodeKind.RootHub => "\uE8CE",        // hub-ish
        UsbNodeKind.ExternalHub => "\uE8CE",
        UsbNodeKind.Device => "\uE88E",         // device
        UsbNodeKind.EmptyPort => "\uE7B3",      // empty slot
        UsbNodeKind.Usb4HostRouter => "\uE945", // lightning / high-speed link
        UsbNodeKind.PnpDevice => "\uE964",      // generic component
        UsbNodeKind.PhysicalGroupRoot => "\uE7F4", // collection
        UsbNodeKind.PhysicalGroup => "\uE977",  // package / enclosure
        _ => "\uE7F4",
    };
}
