namespace UsbInspector.Core.Interop;

/// <summary>
/// USB device I/O control (IOCTL) codes and related constants.
/// These are not exposed by the Win32 metadata, so they are computed here
/// from the classic <c>CTL_CODE</c> macro (see usbioctl.h).
/// </summary>
public static class UsbIoctl
{
    // FILE_DEVICE_USB == FILE_DEVICE_UNKNOWN
    private const uint FILE_DEVICE_USB = 0x00000022;
    private const uint METHOD_BUFFERED = 0;
    private const uint FILE_ANY_ACCESS = 0;

    /// <summary>Classic CTL_CODE macro.</summary>
    public static uint CtlCode(uint deviceType, uint function, uint method, uint access)
        => (deviceType << 16) | (access << 14) | (function << 2) | method;

    private static uint Usb(uint function) => CtlCode(FILE_DEVICE_USB, function, METHOD_BUFFERED, FILE_ANY_ACCESS);

    // Function numbers from usbioctl.h
    private const uint USB_GET_NODE_INFORMATION = 258;                 // also HCD_GET_ROOT_HUB_NAME
    private const uint USB_GET_NODE_CONNECTION_INFORMATION = 259;
    private const uint USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION = 260;
    private const uint USB_GET_NODE_CONNECTION_NAME = 261;
    private const uint USB_GET_NODE_CONNECTION_DRIVERKEY_NAME = 264;
    private const uint USB_GET_HUB_CAPABILITIES = 271;
    private const uint USB_GET_NODE_CONNECTION_INFORMATION_EX = 274;
    private const uint USB_GET_HUB_CAPABILITIES_EX = 276;
    private const uint USB_GET_HUB_INFORMATION_EX = 277;
    private const uint USB_GET_PORT_CONNECTOR_PROPERTIES = 278;
    private const uint USB_GET_NODE_CONNECTION_INFORMATION_EX_V2 = 279;

    // Sent to a USB host controller device object.
    public static readonly uint IOCTL_USB_GET_ROOT_HUB_NAME = Usb(USB_GET_NODE_INFORMATION);

    // Sent to a USB hub device object.
    public static readonly uint IOCTL_USB_GET_NODE_INFORMATION = Usb(USB_GET_NODE_INFORMATION);
    public static readonly uint IOCTL_USB_GET_NODE_CONNECTION_INFORMATION = Usb(USB_GET_NODE_CONNECTION_INFORMATION);
    public static readonly uint IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION = Usb(USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION);
    public static readonly uint IOCTL_USB_GET_NODE_CONNECTION_NAME = Usb(USB_GET_NODE_CONNECTION_NAME);
    public static readonly uint IOCTL_USB_GET_NODE_CONNECTION_DRIVERKEY_NAME = Usb(USB_GET_NODE_CONNECTION_DRIVERKEY_NAME);
    public static readonly uint IOCTL_USB_GET_HUB_CAPABILITIES = Usb(USB_GET_HUB_CAPABILITIES);
    public static readonly uint IOCTL_USB_GET_HUB_CAPABILITIES_EX = Usb(USB_GET_HUB_CAPABILITIES_EX);
    public static readonly uint IOCTL_USB_GET_HUB_INFORMATION_EX = Usb(USB_GET_HUB_INFORMATION_EX);
    public static readonly uint IOCTL_USB_GET_PORT_CONNECTOR_PROPERTIES = Usb(USB_GET_PORT_CONNECTOR_PROPERTIES);
    public static readonly uint IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX = Usb(USB_GET_NODE_CONNECTION_INFORMATION_EX);
    public static readonly uint IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX_V2 = Usb(USB_GET_NODE_CONNECTION_INFORMATION_EX_V2);

    // ---- USB standard descriptor types (usbspec.h) ----
    public const byte USB_DEVICE_DESCRIPTOR_TYPE = 0x01;
    public const byte USB_CONFIGURATION_DESCRIPTOR_TYPE = 0x02;
    public const byte USB_STRING_DESCRIPTOR_TYPE = 0x03;
    public const byte USB_INTERFACE_DESCRIPTOR_TYPE = 0x04;
    public const byte USB_ENDPOINT_DESCRIPTOR_TYPE = 0x05;
    public const byte USB_DEVICE_QUALIFIER_DESCRIPTOR_TYPE = 0x06;
    public const byte USB_BOS_DESCRIPTOR_TYPE = 0x0F;

    // Device capability types found inside a BOS descriptor.
    public const byte USB_DEVICE_CAPABILITY_WIRELESS_USB = 0x01;
    public const byte USB_DEVICE_CAPABILITY_USB20_EXTENSION = 0x02;
    public const byte USB_DEVICE_CAPABILITY_SUPERSPEED_USB = 0x03;
    public const byte USB_DEVICE_CAPABILITY_CONTAINER_ID = 0x04;
    public const byte USB_DEVICE_CAPABILITY_PLATFORM = 0x05;
    public const byte USB_DEVICE_CAPABILITY_POWER_DELIVERY = 0x06;
    public const byte USB_DEVICE_CAPABILITY_BATTERY_INFO = 0x07;
    public const byte USB_DEVICE_CAPABILITY_SUPERSPEEDPLUS_USB = 0x0A;
    public const byte USB_DEVICE_CAPABILITY_BILLBOARD = 0x0D;
    public const byte USB_DEVICE_CAPABILITY_BILLBOARD_EX = 0x10;

    public const int MAXIMUM_USB_STRING_LENGTH = 255;
}
