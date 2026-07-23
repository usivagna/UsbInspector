namespace UsbInspector.Core.Models;

/// <summary>The role a node plays in the USB topology tree.</summary>
public enum UsbNodeKind
{
    HostController,
    RootHub,
    ExternalHub,
    Device,
    EmptyPort,
    Usb4HostRouter,
    Unknown,
}

/// <summary>USB signalling speed, mirroring USB_DEVICE_SPEED plus SuperSpeed tiers.</summary>
public enum UsbSpeed
{
    Unknown = -1,
    Low = 0,        // 1.5 Mbps
    Full = 1,       // 12 Mbps
    High = 2,       // 480 Mbps
    Super = 3,      // 5 Gbps  (USB 3.0 / Gen1x1)
    SuperPlus = 4,  // 10 Gbps (USB 3.1 Gen2x1)
    SuperPlus20 = 5, // 20 Gbps (USB 3.2 Gen2x2)
    Usb4Gen2x2 = 6, // 20 Gbps (USB4)
    Usb4Gen3x2 = 7  // 40 Gbps (USB4)
}

/// <summary>Connection status of a hub port (mirrors USB_CONNECTION_STATUS).</summary>
public enum UsbPortConnectionStatus
{
    NoDeviceConnected = 0,
    DeviceConnected = 1,
    DeviceFailedEnumeration = 2,
    DeviceGeneralFailure = 3,
    DeviceCausedOvercurrent = 4,
    DeviceNotEnoughPower = 5,
    DeviceNotEnoughBandwidth = 6,
    DeviceHubNestedTooDeeply = 7,
    DeviceInLegacyHub = 8,
    DeviceEnumerating = 9,
    DeviceReset = 10,
}
