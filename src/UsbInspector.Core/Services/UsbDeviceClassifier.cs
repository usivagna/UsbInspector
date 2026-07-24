using UsbInspector.Core.Models;

namespace UsbInspector.Core.Services;

/// <summary>A friendly, user-facing category for a USB node.</summary>
public enum UsbDeviceCategory
{
    HostController,
    Hub,
    Keyboard,
    Mouse,
    InputDevice,
    Storage,
    Camera,
    Audio,
    Printer,
    Scanner,
    Network,
    Phone,
    SmartCard,
    Usb4Router,
    Wireless,
    Display,
    Generic,
}

/// <summary>
/// Classifies a <see cref="UsbNode"/> into a friendly <see cref="UsbDeviceCategory"/> using its USB
/// class codes (device descriptor first, then interface classes for composite devices). Pure and
/// unit-testable; the UI maps the category to an icon and a plain-language label.
/// </summary>
public static class UsbDeviceClassifier
{
    public static UsbDeviceCategory Classify(UsbNode node)
    {
        switch (node.Kind)
        {
            case UsbNodeKind.HostController:
                return UsbDeviceCategory.HostController;
            case UsbNodeKind.RootHub:
            case UsbNodeKind.ExternalHub:
                return UsbDeviceCategory.Hub;
            case UsbNodeKind.Usb4HostRouter:
            case UsbNodeKind.Usb4DeviceRouter:
                return UsbDeviceCategory.Usb4Router;
        }

        byte deviceClass = node.DeviceDescriptor?.DeviceClass ?? 0;
        UsbDeviceCategory? fromClass = FromClassCodes(
            deviceClass,
            node.DeviceDescriptor?.DeviceSubClass ?? 0,
            node.DeviceDescriptor?.DeviceProtocol ?? 0);
        if (fromClass is UsbDeviceCategory c && c != UsbDeviceCategory.Generic)
        {
            return c;
        }

        // Composite device (class 0x00) or unhelpful base class: inspect interface classes.
        foreach (UsbConfigurationDescriptorInfo config in node.Configurations)
        {
            foreach (UsbInterfaceDescriptorInfo iface in config.Interfaces)
            {
                UsbDeviceCategory? fromIface = FromClassCodes(iface.InterfaceClass, iface.InterfaceSubClass, iface.InterfaceProtocol);
                if (fromIface is UsbDeviceCategory ic && ic != UsbDeviceCategory.Generic)
                {
                    return ic;
                }
            }
        }

        return UsbDeviceCategory.Generic;
    }

    private static UsbDeviceCategory? FromClassCodes(byte classCode, byte subClass, byte protocol) => classCode switch
    {
        0x01 => UsbDeviceCategory.Audio,
        0x03 => protocol switch
        {
            0x01 => UsbDeviceCategory.Keyboard,
            0x02 => UsbDeviceCategory.Mouse,
            _ => UsbDeviceCategory.InputDevice,
        },
        0x06 => UsbDeviceCategory.Camera, // still image / PTP
        0x07 => UsbDeviceCategory.Printer,
        0x08 => UsbDeviceCategory.Storage,
        0x09 => UsbDeviceCategory.Hub,
        0x0B => UsbDeviceCategory.SmartCard,
        0x0E => UsbDeviceCategory.Camera, // video
        0x02 => UsbDeviceCategory.Network, // communications (CDC)
        0x0A => UsbDeviceCategory.Network, // CDC data
        0x10 => UsbDeviceCategory.Audio, // audio/video
        0xE0 => UsbDeviceCategory.Wireless,
        _ => UsbDeviceCategory.Generic,
    };

    /// <summary>A short, plain-language type label for a category.</summary>
    public static string FriendlyType(UsbDeviceCategory category) => category switch
    {
        UsbDeviceCategory.HostController => "USB controller",
        UsbDeviceCategory.Hub => "USB hub",
        UsbDeviceCategory.Keyboard => "Keyboard",
        UsbDeviceCategory.Mouse => "Mouse",
        UsbDeviceCategory.InputDevice => "Input device",
        UsbDeviceCategory.Storage => "Storage drive",
        UsbDeviceCategory.Camera => "Camera / video",
        UsbDeviceCategory.Audio => "Audio device",
        UsbDeviceCategory.Printer => "Printer",
        UsbDeviceCategory.Scanner => "Scanner",
        UsbDeviceCategory.Network => "Network adapter",
        UsbDeviceCategory.Phone => "Phone",
        UsbDeviceCategory.SmartCard => "Smart card reader",
        UsbDeviceCategory.Usb4Router => "USB4 device",
        UsbDeviceCategory.Wireless => "Wireless adapter",
        UsbDeviceCategory.Display => "Display",
        _ => "USB device",
    };
}
