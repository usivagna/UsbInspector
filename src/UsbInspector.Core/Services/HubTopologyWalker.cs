using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;
using Windows.Win32.Devices.Usb;

namespace UsbInspector.Core.Services;

/// <summary>
/// Recursively walks a hub's ports building the USB topology subtree: connected devices,
/// their descriptors, speed/power, USB-C link info, and nested external hubs.
/// </summary>
public sealed class HubTopologyWalker
{
    private const int MaxDepth = 12;
    private const ushort DefaultLanguageId = 0x0409; // English (US)

    private readonly PnpDeviceIndex _pnpIndex;

    public HubTopologyWalker(PnpDeviceIndex pnpIndex) => _pnpIndex = pnpIndex;

    /// <summary>Populates <paramref name="hubNode"/> with a child node for each populated port.</summary>
    public void WalkHub(UsbNode hubNode, string hubName, int depth = 0)
    {
        if (depth > MaxDepth)
        {
            hubNode.Warnings.Add("Maximum hub nesting depth reached; deeper devices were not enumerated.");
            return;
        }

        using SafeFileHandle hub = OpenHubByName(hubName);
        if (hub.IsInvalid)
        {
            hubNode.Warnings.Add($"Could not open hub '{hubName}' (error {Marshal.GetLastWin32Error()}).");
            return;
        }

        ushort? portCount = HubQueries.GetHighestPortNumber(hub);
        if (portCount is null)
        {
            hubNode.Warnings.Add("Could not read hub port count.");
            return;
        }

        var reader = new DescriptorReader(hub);
        for (uint port = 1; port <= portCount.Value; port++)
        {
            UsbNode? child = BuildPortNode(hub, reader, port, depth);
            if (child is not null)
            {
                hubNode.Children.Add(child);
            }
        }
    }

    private UsbNode? BuildPortNode(SafeFileHandle hub, DescriptorReader reader, uint port, int depth)
    {
        NodeConnection? connection = HubQueries.GetNodeConnection(hub, port);
        if (connection is null)
        {
            return null;
        }

        if (connection.ConnectionStatus != UsbPortConnectionStatus.DeviceConnected)
        {
            // Represent empty / non-enumerated ports so the port layout is visible.
            return new UsbNode
            {
                Kind = UsbNodeKind.EmptyPort,
                Name = $"Port {port}: {DescribeStatus(connection.ConnectionStatus)}",
                PortNumber = port,
                Power = new PortPowerInfo { ConnectionStatus = connection.ConnectionStatus },
            };
        }

        USB_NODE_CONNECTION_INFORMATION_EX_V2_FLAGS? v2Flags = HubQueries.GetNodeConnectionV2Flags(hub, port);
        UsbSpeed speed = MapSpeed(connection.SpeedByte, v2Flags);

        var node = new UsbNode
        {
            Kind = connection.IsHub ? UsbNodeKind.ExternalHub : UsbNodeKind.Device,
            PortNumber = port,
            IsHub = connection.IsHub,
            DeviceAddress = connection.DeviceAddress,
            Speed = speed,
            DeviceDescriptor = connection.DeviceDescriptor,
        };

        ReadDescriptors(node, reader, port);
        AttachPnpProperties(node, hub, port);
        node.UsbC = BuildUsbC(node, v2Flags, speed);
        node.Usb4 = BuildUsb4(node, v2Flags, speed);
        node.Power = BuildPower(node, connection);
        node.Name = BuildDisplayName(node, port);

        if (connection.IsHub)
        {
            string? externalHubName = HubQueries.GetExternalHubName(hub, port);
            if (!string.IsNullOrEmpty(externalHubName))
            {
                WalkHub(node, externalHubName, depth + 1);
            }
            else
            {
                node.Warnings.Add("Device reports as a hub but its hub name could not be read.");
            }
        }

        return node;
    }

    private static void ReadDescriptors(UsbNode node, DescriptorReader reader, uint port)
    {
        UsbDeviceDescriptorInfo? device = node.DeviceDescriptor;
        if (device is null)
        {
            return;
        }

        // Configuration descriptors (with interface/endpoint trees).
        for (byte i = 0; i < device.NumConfigurations; i++)
        {
            UsbConfigurationDescriptorInfo? config = reader.ReadConfiguration(port, i);
            if (config is not null)
            {
                node.Configurations.Add(config);
            }
        }

        // String descriptors: pick the first supported language.
        ushort[] languages = reader.ReadSupportedLanguages(port);
        ushort lang = languages.Length > 0 ? languages[0] : DefaultLanguageId;

        device.Manufacturer = AddString(node, reader, port, device.ManufacturerIndex, lang);
        device.Product = AddString(node, reader, port, device.ProductIndex, lang);
        device.SerialNumber = AddString(node, reader, port, device.SerialNumberIndex, lang);

        foreach (UsbConfigurationDescriptorInfo config in node.Configurations)
        {
            config.Description = AddString(node, reader, port, config.ConfigurationStringIndex, lang);
            foreach (UsbInterfaceDescriptorInfo iface in config.Interfaces)
            {
                iface.Description = AddString(node, reader, port, iface.InterfaceStringIndex, lang);
            }
        }

        // BOS (USB 3.x / USB 2.1+) capability descriptors.
        if (device.BcdUsb >= 0x0210)
        {
            node.BosCapabilities.AddRange(reader.ReadBos(port));
        }

        if (node.Configurations.Count == 0)
        {
            node.Warnings.Add("Configuration descriptors could not be read (elevation may be required).");
        }
    }

    private static string? AddString(UsbNode node, DescriptorReader reader, uint port, byte index, ushort lang)
    {
        if (index == 0)
        {
            return null;
        }

        string? value = reader.ReadStringDescriptor(port, index, lang);
        if (!string.IsNullOrEmpty(value))
        {
            node.StringDescriptors.Add(new UsbStringDescriptorInfo { Index = index, LanguageId = lang, Value = value });
        }

        return value;
    }

    private void AttachPnpProperties(UsbNode node, SafeFileHandle hub, uint port)
    {
        string? driverKey = HubQueries.GetDriverKeyName(hub, port);
        uint? devInst = _pnpIndex.Resolve(driverKey);
        if (devInst is uint di)
        {
            node.DevInst = di;
            node.Pnp = PnpPropertyReader.Read(di);
            node.InstanceId = node.Pnp.InstanceId;
        }
        else if (!string.IsNullOrEmpty(driverKey))
        {
            node.Warnings.Add($"Could not map device to a PnP node (driver key '{driverKey}').");
        }
    }

    private static UsbCInfo BuildUsbC(UsbNode node, USB_NODE_CONNECTION_INFORMATION_EX_V2_FLAGS? v2Flags, UsbSpeed speed)
    {
        var usbc = new UsbCInfo();

        if (v2Flags is { } flags)
        {
            usbc.SupportsSuperSpeedPlus = flags.Anonymous.DeviceIsSuperSpeedPlusCapableOrHigher;
            if (flags.Anonymous.DeviceIsOperatingAtSuperSpeedPlusOrHigher)
            {
                usbc.SupportedLinkModes.Add("Operating at SuperSpeedPlus (10+ Gbps)");
            }
            else if (flags.Anonymous.DeviceIsOperatingAtSuperSpeedOrHigher)
            {
                usbc.SupportedLinkModes.Add("Operating at SuperSpeed (5 Gbps)");
            }

            if (flags.Anonymous.DeviceIsSuperSpeedPlusCapableOrHigher)
            {
                usbc.SupportedLinkModes.Add("Capable of SuperSpeedPlus");
            }
            else if (flags.Anonymous.DeviceIsSuperSpeedCapableOrHigher)
            {
                usbc.SupportedLinkModes.Add("Capable of SuperSpeed");
            }
        }

        usbc.NegotiatedLinkSpeed = DescribeSpeed(speed);

        // Billboard capability (advertised over a USB-C connector for alternate modes).
        foreach (UsbBosCapabilityInfo cap in node.BosCapabilities)
        {
            if (cap.CapabilityType == UsbIoctl.USB_DEVICE_CAPABILITY_BILLBOARD)
            {
                usbc.HasBillboardCapability = true;
                usbc.IsTypeCConnector = true;
                ParseBillboardAltModes(cap.RawData, usbc);
            }
        }

        usbc.Limitations.Add("USB Power Delivery contract details (raw PDOs) are not exposed to user mode by Windows.");
        if (!usbc.IsTypeCConnector)
        {
            usbc.Limitations.Add("Physical connector type (Type-C vs legacy) is not reported per-device by Windows public APIs.");
        }

        return usbc;
    }

    private static Usb4Info? BuildUsb4(UsbNode node, USB_NODE_CONNECTION_INFORMATION_EX_V2_FLAGS? v2Flags, UsbSpeed speed)
    {
        var usb4 = new Usb4Info();

        // Devices attached over a USB4 fabric tunnel USB 3.2 traffic; the tunnelled link presents
        // as SuperSpeed/SuperSpeedPlus (10/20 Gbps) via the EX_V2 flags.
        if (v2Flags is { } flags && flags.Anonymous.DeviceIsOperatingAtSuperSpeedPlusOrHigher)
        {
            usb4.SupportsTunnelledUsb = true;
            usb4.Capabilities.Add("Operating at SuperSpeedPlus (10+ Gbps) — compatible with tunnelled USB over USB4.");
        }

        // A USB4-class negotiated speed (20/40 Gbps) is a strong USB4 signal where Windows reports it.
        if (speed is UsbSpeed.Usb4Gen2x2 or UsbSpeed.Usb4Gen3x2)
        {
            usb4.IsUsb4Capable = true;
            usb4.LinkSpeedLabel = DescribeSpeed(speed);
        }

        // Hardware/compatible IDs sometimes carry a USB4 marker for the peripheral.
        if (node.Pnp is { } pnp
            && (pnp.HardwareIds.Concat(pnp.CompatibleIds)
                    .Any(id => id.Contains("USB4", StringComparison.OrdinalIgnoreCase))
                || (pnp.BusReportedDeviceDesc?.Contains("USB4", StringComparison.OrdinalIgnoreCase) ?? false)))
        {
            usb4.IsUsb4Capable = true;
        }

        if (!usb4.HasAnyData)
        {
            return null;
        }

        usb4.Limitations.Add(
            "USB4 fabric detail (routers/adapters, PCIe/DisplayPort tunnelling, per-lane negotiation) " +
            "is not exposed per-device to user mode by public Windows APIs.");
        return usb4;
    }

    private static void ParseBillboardAltModes(byte[] raw, UsbCInfo usbc)
    {
        // USB Billboard Capability Descriptor: alt-mode array begins at offset 44,
        // each entry is { wSVID(2), bAlternateMode(1), iAlternateModeString(1) }.
        const int altModeArrayOffset = 44;
        const int entrySize = 4;
        if (raw.Length < 5)
        {
            return;
        }

        int numAltModes = raw[4];
        for (int i = 0; i < numAltModes; i++)
        {
            int off = altModeArrayOffset + (i * entrySize);
            if (off + entrySize > raw.Length)
            {
                break;
            }

            usbc.AlternateModes.Add(new UsbAltModeInfo
            {
                SVID = (ushort)(raw[off] | (raw[off + 1] << 8)),
                Index = raw[off + 2],
                Name = raw[off] == 0xFF && raw[off + 1] == 0xFF ? "DisplayPort" : null,
            });
        }
    }

    private static PortPowerInfo BuildPower(UsbNode node, NodeConnection connection)
    {
        UsbConfigurationDescriptorInfo? current = node.Configurations
            .FirstOrDefault(c => c.ConfigurationValue == connection.CurrentConfigurationValue)
            ?? node.Configurations.FirstOrDefault();

        return new PortPowerInfo
        {
            RequiredMilliAmps = current?.MaxPowerMilliAmps,
            SelfPowered = current?.SelfPowered,
            RemoteWakeupCapable = current?.RemoteWakeup,
            ConnectionStatus = connection.ConnectionStatus,
        };
    }

    private static string BuildDisplayName(UsbNode node, uint port)
    {
        string? friendly = node.Pnp?.BestDisplayName;
        string? product = node.DeviceDescriptor?.Product;
        string label = product
            ?? (friendly is not null && friendly != node.InstanceId ? friendly : null)
            ?? (node.IsHub ? "USB Hub" : "USB Device");

        return $"Port {port}: {label}";
    }

    private static SafeFileHandle OpenHubByName(string hubName)
    {
        string path = hubName.StartsWith(@"\\", StringComparison.Ordinal) ? hubName : @"\\.\" + hubName;
        return NativeDevice.Open(path, writeAccess: true);
    }

    private static UsbSpeed MapSpeed(byte speedByte, USB_NODE_CONNECTION_INFORMATION_EX_V2_FLAGS? v2Flags)
    {
        UsbSpeed baseSpeed = speedByte switch
        {
            0 => UsbSpeed.Low,
            1 => UsbSpeed.Full,
            2 => UsbSpeed.High,
            3 => UsbSpeed.Super,
            _ => UsbSpeed.Unknown,
        };

        if (v2Flags is { } f)
        {
            if (f.Anonymous.DeviceIsOperatingAtSuperSpeedPlusOrHigher)
            {
                return UsbSpeed.SuperPlus;
            }

            if (f.Anonymous.DeviceIsOperatingAtSuperSpeedOrHigher && baseSpeed < UsbSpeed.Super)
            {
                return UsbSpeed.Super;
            }
        }

        return baseSpeed;
    }

    private static string DescribeSpeed(UsbSpeed speed) => speed switch
    {
        UsbSpeed.Low => "Low Speed (1.5 Mbps)",
        UsbSpeed.Full => "Full Speed (12 Mbps)",
        UsbSpeed.High => "High Speed (480 Mbps)",
        UsbSpeed.Super => "SuperSpeed (5 Gbps)",
        UsbSpeed.SuperPlus => "SuperSpeed+ (10 Gbps)",
        UsbSpeed.SuperPlus20 => "SuperSpeed+ (20 Gbps)",
        UsbSpeed.Usb4Gen2x2 => "USB4 (20 Gbps)",
        UsbSpeed.Usb4Gen3x2 => "USB4 (40 Gbps)",
        _ => "Unknown",
    };

    private static string DescribeStatus(UsbPortConnectionStatus status) => status switch
    {
        UsbPortConnectionStatus.NoDeviceConnected => "(empty)",
        UsbPortConnectionStatus.DeviceFailedEnumeration => "device failed enumeration",
        UsbPortConnectionStatus.DeviceGeneralFailure => "device general failure",
        UsbPortConnectionStatus.DeviceCausedOvercurrent => "device caused overcurrent",
        UsbPortConnectionStatus.DeviceNotEnoughPower => "not enough power",
        UsbPortConnectionStatus.DeviceNotEnoughBandwidth => "not enough bandwidth",
        UsbPortConnectionStatus.DeviceHubNestedTooDeeply => "hub nested too deeply",
        UsbPortConnectionStatus.DeviceInLegacyHub => "in legacy hub",
        UsbPortConnectionStatus.DeviceEnumerating => "enumerating",
        _ => status.ToString(),
    };
}
