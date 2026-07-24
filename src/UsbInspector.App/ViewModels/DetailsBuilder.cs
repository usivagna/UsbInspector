using System.Text;
using UsbInspector.Core.Models;
using UsbInspector.Core.Services;

namespace UsbInspector_App.ViewModels;

/// <summary>Builds the detail-pane sections (rows and text) for a selected USB node.</summary>
public static class DetailsBuilder
{
    public static IEnumerable<DetailRow> Overview(UsbNode node)
    {
        UsbDeviceCategory category = UsbDeviceClassifier.Classify(node);
        yield return new DetailRow("Device type", UsbDeviceClassifier.FriendlyType(category));
        yield return new DetailRow("Name", node.Name);
        if (node.PortNumber is uint port)
        {
            yield return new DetailRow("Port", port.ToString(), "Which numbered port on the parent hub this device is plugged into.");
        }

        yield return new DetailRow("Speed", DescribeSpeed(node.Speed), "How fast this device can transfer data.");
        if (node.DeviceAddress is ushort addr)
        {
            yield return new DetailRow("Device address", addr.ToString());
        }

        UsbDeviceDescriptorInfo? d = node.DeviceDescriptor;
        if (d is not null)
        {
            yield return new DetailRow("Vendor ID", $"{d.VendorIdHex} ({d.IdVendor})", "The maker's USB ID number.");
            yield return new DetailRow("Product ID", $"{d.ProductIdHex} ({d.IdProduct})", "The product's USB ID number.");
            yield return new DetailRow("USB version", d.UsbVersion);
            yield return new DetailRow("Device release", $"0x{d.BcdDevice:X4}");
            yield return new DetailRow("Class", $"0x{d.DeviceClass:X2}");
            yield return new DetailRow("Manufacturer", d.Manufacturer);
            yield return new DetailRow("Product", d.Product);
            yield return new DetailRow("Serial number", d.SerialNumber);
            yield return new DetailRow("Configurations", d.NumConfigurations.ToString());
        }

        yield return new DetailRow("Identifier", node.InstanceId, "Windows' internal name for this device.");
    }

    public static IEnumerable<DetailRow> Pnp(UsbNode node)
    {
        PnpDeviceProperties? p = node.Pnp;
        if (p is null)
        {
            yield return new DetailRow("PnP data", "Not available for this node.");
            yield break;
        }

        yield return new DetailRow("Device description", p.DeviceDescription);
        yield return new DetailRow("Friendly name", p.FriendlyName);
        yield return new DetailRow("Bus reported desc", p.BusReportedDeviceDesc);
        yield return new DetailRow("Manufacturer", p.Manufacturer);
        yield return new DetailRow("Class", p.Class);
        yield return new DetailRow("Service (driver)", p.Service);
        yield return new DetailRow("Driver key", p.Driver);
        yield return new DetailRow("Driver version", p.DriverVersion);
        yield return new DetailRow("Driver date", p.DriverDate);
        yield return new DetailRow("Driver provider", p.DriverProvider);
        yield return new DetailRow("Container ID", p.ContainerId);
        yield return new DetailRow("Location", p.LocationInfo);
        yield return new DetailRow("PDO name", p.PdoName);
        yield return new DetailRow("Hardware IDs", string.Join(Environment.NewLine, p.HardwareIds));
        yield return new DetailRow("Compatible IDs", string.Join(Environment.NewLine, p.CompatibleIds));
        yield return new DetailRow("Present", p.IsPresent?.ToString());
        if (p.ProblemCode is uint pc && pc != 0)
        {
            yield return new DetailRow("Problem", $"{pc} {p.ProblemDescription}");
        }
    }

    public static IEnumerable<DetailRow> Power(UsbNode node)
    {
        PortPowerInfo? p = node.Power;
        yield return new DetailRow("Speed", DescribeSpeed(node.Speed));
        if (p is null)
        {
            yield break;
        }

        yield return new DetailRow("Required current", p.RequiredMilliAmps is int ma ? $"{ma} mA" : null);
        yield return new DetailRow("Self powered", p.SelfPowered?.ToString());
        yield return new DetailRow("Remote wakeup", p.RemoteWakeupCapable?.ToString());
        yield return new DetailRow("Connection status", p.ConnectionStatus?.ToString());
    }

    public static IEnumerable<DetailRow> UsbC(UsbNode node)
    {
        UsbCInfo? c = node.UsbC;
        if (c is null || !c.HasAnyData)
        {
            yield return new DetailRow("USB-C / Type-C", "No USB-C details reported for this device.");
            if (c is not null)
            {
                foreach (string note in c.Limitations)
                {
                    yield return new DetailRow("Note", note);
                }
            }

            yield break;
        }

        yield return new DetailRow("Negotiated link speed", c.NegotiatedLinkSpeed);
        yield return new DetailRow("SuperSpeedPlus capable", c.SupportsSuperSpeedPlus.ToString());
        yield return new DetailRow("Billboard (alt modes)", c.HasBillboardCapability.ToString());
        yield return new DetailRow("Type-C connector", c.IsTypeCConnector.ToString());

        foreach (string mode in c.SupportedLinkModes)
        {
            yield return new DetailRow("Link mode", mode);
        }

        foreach (UsbAltModeInfo alt in c.AlternateModes)
        {
            yield return new DetailRow("Alternate mode", $"SVID {alt.SvidHex}{(alt.Name is not null ? $" ({alt.Name})" : "")}, index {alt.Index}");
        }

        foreach (string note in c.Limitations)
        {
            yield return new DetailRow("Note", note);
        }
    }

    public static IEnumerable<DetailRow> Usb4(UsbNode node)
    {
        Usb4Info? u = node.Usb4;
        if (u is null || !u.HasAnyData)
        {
            yield return new DetailRow("USB4", "This device doesn't use USB4.");
            if (u is not null)
            {
                foreach (string note in u.Limitations)
                {
                    yield return new DetailRow("Note", note);
                }
            }

            yield break;
        }

        yield return new DetailRow("USB4 host router", u.IsHostRouter.ToString());
        yield return new DetailRow("USB4 capable", u.IsUsb4Capable.ToString());

        if (u.RouterName is not null)
        {
            yield return new DetailRow("Router name", u.RouterName);
        }

        if (u.HostRouterInstanceId is not null)
        {
            yield return new DetailRow("Host router instance", u.HostRouterInstanceId);
        }

        if (u.LinkSpeedLabel is not null)
        {
            yield return new DetailRow("Link speed", u.LinkSpeedLabel);
        }

        yield return new DetailRow("Tunnelled USB", u.SupportsTunnelledUsb.ToString());

        if (u.HasFabricData)
        {
            if (u.DomainId is uint dom)
            {
                yield return new DetailRow("Domain ID", $"0x{dom:X}", "Which USB4 bus (host router group) this device belongs to.");
            }

            if (u.TopologyId is not null)
            {
                yield return new DetailRow("Topology ID", u.TopologyId, "The device's position on the USB4 chain (host → hub → device).");
            }

            if (u.SiliconVendorId is ushort sv)
            {
                yield return new DetailRow("Silicon vendor ID", $"0x{sv:X4}", "The maker of the USB4 controller chip.");
            }

            if (u.SiliconProductId is ushort sp)
            {
                yield return new DetailRow("Silicon product ID", $"0x{sp:X4}");
            }

            if (u.SiliconRevision is uint sr)
            {
                yield return new DetailRow("Silicon revision", sr.ToString());
            }

            if (u.Usb4Version is not null)
            {
                yield return new DetailRow("USB4 version", u.Usb4Version);
            }

            if (u.Uuid is not null)
            {
                yield return new DetailRow("UUID", u.Uuid);
            }

            if (u.AsciiVendorName is not null)
            {
                yield return new DetailRow("Vendor name", u.AsciiVendorName);
            }

            if (u.AsciiModelName is not null)
            {
                yield return new DetailRow("Model name", u.AsciiModelName);
            }

            if (u.UnitVendorId is ushort uv)
            {
                yield return new DetailRow("Unit vendor ID", $"0x{uv:X4}");
            }

            if (u.UnitProductId is ushort up)
            {
                yield return new DetailRow("Unit product ID", $"0x{up:X4}");
            }

            if (u.FirmwareVersion is not null)
            {
                yield return new DetailRow("Firmware version", u.FirmwareVersion);
            }

            if (u.CurrentBandwidthDownGbps is double down && u.CurrentBandwidthUpGbps is double up2)
            {
                string gen = u.LinkGeneration is int g ? $" (Gen {g}, {(u.LanesBonded == true ? "dual" : "single")} lane)" : string.Empty;
                yield return new DetailRow("Current bandwidth (down/up)", $"{down:0}Gbps/{up2:0}Gbps{gen}", "The negotiated USB4 link speed for this connection.");
            }

            if (u.DpInAdaptersTotal is int dpt)
            {
                yield return new DetailRow("Total DP IN adapters", dpt.ToString(), "How many DisplayPort video inputs this router has.");
            }

            if (u.DpInAdaptersTunneled is int dptun)
            {
                yield return new DetailRow("Tunneled DP IN adapters", dptun.ToString(), "DisplayPort inputs currently carrying video over USB4.");
            }

            if (u.DpInAdaptersUnavailable is int dpun)
            {
                yield return new DetailRow("Unavailable DP IN adapters", dpun.ToString());
            }
        }
        else if (u.FabricDetailRequiresElevation)
        {
            yield return new DetailRow(
                "Fabric detail",
                "Full USB4 fabric information (Domain/Topology IDs, silicon IDs, model/firmware, bandwidth) "
                + "requires administrator rights. Use 'Restart as Admin' to match the Windows Settings "
                + "'USB4 hubs and devices' page.");
        }

        foreach (string cap in u.Capabilities)
        {
            yield return new DetailRow("Capability", cap);
        }

        foreach (string note in u.Limitations)
        {
            yield return new DetailRow("Note", note);
        }
    }

    /// <summary>
    /// The unified "Also here" view: other functions that live on the same physical port/connector,
    /// then other functions of the same physical device/enclosure. In Advanced mode the raw keys and
    /// grouping-confidence notes are appended.
    /// </summary>
    public static IEnumerable<DetailRow> AlsoHere(UsbNode node, UsbSnapshot? snapshot, bool advanced)
    {
        if (snapshot is null)
        {
            yield return new DetailRow("Also here", "Run a scan to see related functions.");
            yield break;
        }

        bool foundAny = false;

        // 1) Same physical port / connector.
        (string Key, PortGroupSource Source)? port = PhysicalPortGrouper.ComputePortKey(node);
        if (port is not null)
        {
            List<UsbNode> sharingPort = snapshot.EnumerateAll()
                .Where(n => !ReferenceEquals(n, node))
                .Where(n =>
                {
                    var c = PhysicalPortGrouper.ComputePortKey(n);
                    return c is not null && string.Equals(c.Value.Key, port.Value.Key, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (sharingPort.Count > 0)
            {
                foundAny = true;
                bool confident = port.Value.Source is PortGroupSource.Usb4FabricPort or PortGroupSource.AcpiPld;
                yield return new DetailRow(
                    "On the same port",
                    $"{sharingPort.Count} other function(s)  ({(confident ? "✓ Confirmed" : "~ Likely")})",
                    "Other USB functions that share this physical connector (e.g. USB3 + USB4 on one USB-C port).");
                foreach (UsbNode r in sharingPort)
                {
                    yield return new DetailRow("• " + UsbDeviceClassifier.FriendlyType(UsbDeviceClassifier.Classify(r)),
                        DisplayName(r, advanced));
                }

                if (advanced)
                {
                    yield return new DetailRow("Port key", port.Value.Key);
                }
            }
        }

        // 2) Same physical device / enclosure (ContainerId).
        string? containerId = node.Pnp?.ContainerId;
        if (UsbSnapshot.IsRealContainerId(containerId))
        {
            List<UsbNode> sameDevice = snapshot.EnumerateAll()
                .Where(n => !ReferenceEquals(n, node)
                            && string.Equals(n.Pnp?.ContainerId, containerId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sameDevice.Count > 0)
            {
                foundAny = true;
                yield return new DetailRow(
                    "Part of the same device",
                    $"{sameDevice.Count} other function(s)",
                    "Other functions that belong to the same physical gadget/enclosure.");
                foreach (UsbNode r in sameDevice)
                {
                    yield return new DetailRow("• " + UsbDeviceClassifier.FriendlyType(UsbDeviceClassifier.Classify(r)),
                        DisplayName(r, advanced));
                }

                if (advanced)
                {
                    yield return new DetailRow("Container ID", containerId);
                }
            }
        }

        if (!foundAny)
        {
            yield return new DetailRow("Also here", "Nothing else shares this device's port or enclosure.");
        }
    }

    private static string DisplayName(UsbNode node, bool advanced)
        => node.Name + (advanced && node.InstanceId is not null ? $"  [{node.InstanceId}]" : string.Empty);

    public static string Descriptors(UsbNode node)
    {
        var sb = new StringBuilder();
        UsbDeviceDescriptorInfo? d = node.DeviceDescriptor;
        if (d is not null)
        {
            sb.AppendLine("DEVICE DESCRIPTOR");
            sb.AppendLine($"  bcdUSB              {d.UsbVersion}");
            sb.AppendLine($"  bDeviceClass        0x{d.DeviceClass:X2}");
            sb.AppendLine($"  bDeviceSubClass     0x{d.DeviceSubClass:X2}");
            sb.AppendLine($"  bDeviceProtocol     0x{d.DeviceProtocol:X2}");
            sb.AppendLine($"  bMaxPacketSize0     {d.MaxPacketSize0}");
            sb.AppendLine($"  idVendor            {d.VendorIdHex}");
            sb.AppendLine($"  idProduct           {d.ProductIdHex}");
            sb.AppendLine($"  bcdDevice           0x{d.BcdDevice:X4}");
            sb.AppendLine($"  iManufacturer       {d.ManufacturerIndex}  {d.Manufacturer}");
            sb.AppendLine($"  iProduct            {d.ProductIndex}  {d.Product}");
            sb.AppendLine($"  iSerialNumber       {d.SerialNumberIndex}  {d.SerialNumber}");
            sb.AppendLine($"  bNumConfigurations  {d.NumConfigurations}");
            sb.AppendLine();
        }

        foreach (UsbConfigurationDescriptorInfo config in node.Configurations)
        {
            sb.AppendLine($"CONFIGURATION {config.ConfigurationValue}  {config.Description}");
            sb.AppendLine($"  bNumInterfaces      {config.NumInterfaces}");
            sb.AppendLine($"  bmAttributes        0x{config.Attributes:X2} (self-powered={config.SelfPowered}, remote-wakeup={config.RemoteWakeup})");
            sb.AppendLine($"  MaxPower            {config.MaxPowerMilliAmps} mA");
            foreach (UsbInterfaceDescriptorInfo iface in config.Interfaces)
            {
                sb.AppendLine($"  INTERFACE {iface.InterfaceNumber}.{iface.AlternateSetting}  {iface.ClassName}  {iface.Description}");
                sb.AppendLine($"    class/subclass/proto  0x{iface.InterfaceClass:X2}/0x{iface.InterfaceSubClass:X2}/0x{iface.InterfaceProtocol:X2}");
                foreach (UsbEndpointDescriptorInfo ep in iface.Endpoints)
                {
                    sb.AppendLine($"    ENDPOINT 0x{ep.EndpointAddress:X2}  {ep.Direction}  {ep.TransferType}  maxPacket={ep.MaxPacketSize}  interval={ep.Interval}");
                }
            }

            sb.AppendLine();
        }

        if (node.BosCapabilities.Count > 0)
        {
            sb.AppendLine("BOS (USB 3.x) CAPABILITIES");
            foreach (UsbBosCapabilityInfo cap in node.BosCapabilities)
            {
                sb.AppendLine($"  {cap.CapabilityName}  {cap.Summary}");
            }

            sb.AppendLine();
        }

        if (node.StringDescriptors.Count > 0)
        {
            sb.AppendLine("STRING DESCRIPTORS");
            foreach (UsbStringDescriptorInfo s in node.StringDescriptors)
            {
                sb.AppendLine($"  [{s.Index}] (lang 0x{s.LanguageId:X4})  {s.Value}");
            }
        }

        if (sb.Length == 0)
        {
            sb.AppendLine("No descriptors available for this node.");
        }

        return sb.ToString();
    }

    public static string Raw(UsbNode node)
    {
        var sb = new StringBuilder();
        if (node.Pnp is { AllProperties.Count: > 0 } pnp)
        {
            sb.AppendLine("ALL PNP PROPERTIES");
            foreach (KeyValuePair<string, string> kv in pnp.AllProperties.OrderBy(k => k.Key))
            {
                sb.AppendLine($"  {kv.Key}");
                sb.AppendLine($"      {kv.Value}");
            }

            sb.AppendLine();
        }

        if (node.BosCapabilities.Count > 0)
        {
            sb.AppendLine("BOS RAW BYTES");
            foreach (UsbBosCapabilityInfo cap in node.BosCapabilities)
            {
                sb.AppendLine($"  {cap.CapabilityName}: {Convert.ToHexString(cap.RawData)}");
            }

            sb.AppendLine();
        }

        if (node.Warnings.Count > 0)
        {
            sb.AppendLine("WARNINGS");
            foreach (string w in node.Warnings)
            {
                sb.AppendLine($"  {w}");
            }
        }

        if (sb.Length == 0)
        {
            sb.AppendLine("No raw data available for this node.");
        }

        return sb.ToString();
    }

    public static string DescribeSpeed(UsbSpeed speed) => speed switch
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
}
