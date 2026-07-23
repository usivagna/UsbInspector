using System.Text;
using UsbInspector.Core.Models;

namespace UsbInspector_App.ViewModels;

/// <summary>Builds the detail-pane sections (rows and text) for a selected USB node.</summary>
public static class DetailsBuilder
{
    public static IEnumerable<DetailRow> Overview(UsbNode node)
    {
        yield return new DetailRow("Type", node.Kind.ToString());
        yield return new DetailRow("Name", node.Name);
        if (node.PortNumber is uint port)
        {
            yield return new DetailRow("Port", port.ToString());
        }

        yield return new DetailRow("Speed", DescribeSpeed(node.Speed));
        if (node.DeviceAddress is ushort addr)
        {
            yield return new DetailRow("Device address", addr.ToString());
        }

        UsbDeviceDescriptorInfo? d = node.DeviceDescriptor;
        if (d is not null)
        {
            yield return new DetailRow("Vendor ID", $"{d.VendorIdHex} ({d.IdVendor})");
            yield return new DetailRow("Product ID", $"{d.ProductIdHex} ({d.IdProduct})");
            yield return new DetailRow("USB version", d.UsbVersion);
            yield return new DetailRow("Device release", $"0x{d.BcdDevice:X4}");
            yield return new DetailRow("Class", $"0x{d.DeviceClass:X2}");
            yield return new DetailRow("Manufacturer", d.Manufacturer);
            yield return new DetailRow("Product", d.Product);
            yield return new DetailRow("Serial number", d.SerialNumber);
            yield return new DetailRow("Configurations", d.NumConfigurations.ToString());
        }

        yield return new DetailRow("Instance ID", node.InstanceId);
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
            yield return new DetailRow("USB-C / Type-C", "No USB-C specific data reported for this device.");
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
            yield return new DetailRow("USB4", "No USB4-specific data reported for this device.");
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

        foreach (string cap in u.Capabilities)
        {
            yield return new DetailRow("Capability", cap);
        }

        foreach (string note in u.Limitations)
        {
            yield return new DetailRow("Note", note);
        }
    }

    public static IEnumerable<DetailRow> Related(UsbNode node, UsbSnapshot? snapshot)
    {
        string? containerId = node.Pnp?.ContainerId;
        if (snapshot is null || !UsbSnapshot.IsRealContainerId(containerId))
        {
            yield return new DetailRow("Related devices", "No physical-device grouping (ContainerId) reported for this node.");
            yield break;
        }

        yield return new DetailRow("Container ID", containerId);

        List<UsbNode> related = snapshot.EnumerateAll()
            .Where(n => !ReferenceEquals(n, node)
                        && string.Equals(n.Pnp?.ContainerId, containerId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (related.Count == 0)
        {
            yield return new DetailRow("Related devices", "This is the only function reported for its physical container.");
        }
        else
        {
            foreach (UsbNode r in related)
            {
                string detail = r.Name + (r.InstanceId is not null ? $"  [{r.InstanceId}]" : string.Empty);
                yield return new DetailRow(r.Kind.ToString(), detail);
            }
        }

        yield return new DetailRow(
            "Note",
            "Grouped by Windows ContainerId — the logically-separate USB3/USB4 functions of one "
            + "physical device/enclosure. True same-connector identity (ACPI _PLD/_UPC) is not exposed to user mode.");
    }

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
