using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;
using Windows.Win32;

namespace UsbInspector.Core.Services;

/// <summary>
/// Discovers USB4 host router devnodes. Windows does not publish a device-interface class GUID for
/// USB4 host routers, so they are located by scanning present devnodes and matching their service /
/// device class / description (driver <c>Usb4HostRouter.sys</c>, description "USB4(TM) Host Router").
/// </summary>
public sealed class Usb4Enumerator
{
    private const string HostRouterLimitation =
        "Full USB4 fabric detail (router/adapter topology, PCIe/DisplayPort tunnelling, per-lane " +
        "negotiation and native link state) is not exposed to user mode by public Windows APIs.";

    public IReadOnlyList<UsbNode> Enumerate(List<string> warnings)
    {
        var routers = new List<UsbNode>();

        try
        {
            foreach (DeviceInfoInstance instance in DeviceInfoEnumerator.EnumerateAllPresent())
            {
                if (!LooksLikeUsb4HostRouter(instance.DevInst))
                {
                    continue;
                }

                routers.Add(BuildRouter(instance));
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"USB4 host router scan failed: {ex.Message}");
        }

        return routers;
    }

    private static bool LooksLikeUsb4HostRouter(uint devInst)
    {
        string? service = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_Service);
        if (Contains(service, "usb4") && (Contains(service, "router") || Contains(service, "host")))
        {
            return true;
        }

        string? deviceClass = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_Class);
        if (Contains(deviceClass, "usb4"))
        {
            return true;
        }

        string? desc = DevProp.GetString(devInst, PInvoke.DEVPKEY_Device_DeviceDesc);
        return Contains(desc, "usb4") && Contains(desc, "router");
    }

    private static UsbNode BuildRouter(DeviceInfoInstance instance)
    {
        PnpDeviceProperties pnp = PnpPropertyReader.Read(instance.DevInst);

        var usb4 = new Usb4Info
        {
            IsUsb4Capable = true,
            IsHostRouter = true,
            RouterName = pnp.BestDisplayName,
            HostRouterInstanceId = pnp.InstanceId ?? instance.InstanceId,
            LinkSpeedLabel = "USB4 (up to 40 Gbps)",
        };

        if (!string.IsNullOrEmpty(pnp.DriverProvider) || !string.IsNullOrEmpty(pnp.Service))
        {
            usb4.Capabilities.Add(
                $"USB4 host router driver: {pnp.Service ?? "?"}"
                + (pnp.DriverVersion is not null ? $" v{pnp.DriverVersion}" : string.Empty));
        }

        usb4.Capabilities.Add("Tunnels USB 3.2, DisplayPort and (where supported) PCIe over the USB4 fabric.");
        usb4.Limitations.Add(HostRouterLimitation);

        return new UsbNode
        {
            Kind = UsbNodeKind.Usb4HostRouter,
            Name = $"USB4 Host Router: {pnp.BestDisplayName}",
            InstanceId = pnp.InstanceId ?? instance.InstanceId,
            DevInst = instance.DevInst,
            Pnp = pnp,
            Usb4 = usb4,
        };
    }

    private static bool Contains(string? value, string token)
        => value is not null && value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
