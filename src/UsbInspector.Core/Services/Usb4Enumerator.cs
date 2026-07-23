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

    // A USB4 host router is a PCIe/PnP system device (not a USB hub), so its topology can only be
    // walked via the Configuration Manager devnode tree, not the USB hub/port IOCTLs. Depth-capped
    // and cycle-guarded so a pathological devnode graph can never hang or overflow.
    private const int MaxSubtreeDepth = 32;

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

                UsbNode router = BuildRouter(instance);
                PopulateSubtree(router, instance.DevInst, warnings);
                routers.Add(router);
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"USB4 host router scan failed: {ex.Message}");
        }

        EnrichWithFabricRundown(routers, warnings);

        return routers;
    }

    /// <summary>
    /// Enriches the enumerated host routers with USB4 fabric detail from the connection-manager ETW
    /// rundown (the same source the Windows Settings "USB4 hubs and devices" page uses). Best-effort:
    /// requires elevation, and when unavailable the PnP subtree remains as the fallback view.
    /// </summary>
    private static void EnrichWithFabricRundown(List<UsbNode> routers, List<string> warnings)
    {
        if (routers.Count == 0)
        {
            return;
        }

        try
        {
            Usb4RundownData data = new Usb4RundownReader().Read(warnings);
            new Usb4FabricEnricher().Enrich(routers, data, warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"USB4 fabric enrichment failed: {ex.Message}");
        }
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

    /// <summary>
    /// Walks the router's PnP/PCIe devnode subtree (tunnelled xHCI host controllers, PCIe/DisplayPort
    /// tunnels and their descendants) via CfgMgr32, attaching each descendant as a child node. This is
    /// a PnP view, not a USB hub/port tree; the tunnelled USB devices themselves still appear under the
    /// tunnelled xHCI host controller in the host-controller tree (cross-linked by ContainerId).
    /// </summary>
    private static void PopulateSubtree(UsbNode parent, uint parentDevInst, List<string> warnings)
    {
        try
        {
            var visited = new HashSet<uint>();
            AddChildren(parent, parentDevInst, visited, 0);
        }
        catch (Exception ex)
        {
            parent.Warnings.Add($"Could not enumerate USB4 router subtree: {ex.Message}");
        }
    }

    private static void AddChildren(UsbNode parentNode, uint parentDevInst, HashSet<uint> visited, int depth)
    {
        if (depth >= MaxSubtreeDepth)
        {
            parentNode.Warnings.Add("Maximum devnode nesting depth reached; deeper devices were not enumerated.");
            return;
        }

        uint? child = CmDevice.GetChild(parentDevInst);
        while (child is uint devInst)
        {
            if (visited.Add(devInst))
            {
                UsbNode childNode = BuildDevnode(devInst);
                parentNode.Children.Add(childNode);
                AddChildren(childNode, devInst, visited, depth + 1);
            }

            child = CmDevice.GetSibling(devInst);
        }
    }

    private static UsbNode BuildDevnode(uint devInst)
    {
        PnpDeviceProperties pnp;
        try
        {
            pnp = PnpPropertyReader.Read(devInst);
        }
        catch
        {
            pnp = new PnpDeviceProperties { InstanceId = CmDevice.GetDeviceId(devInst) };
        }

        bool isTunnelledUsbController = LooksLikeUsbHostController(pnp);
        string name = isTunnelledUsbController
            ? $"Tunnelled USB controller: {pnp.BestDisplayName}"
            : pnp.BestDisplayName;

        var node = new UsbNode
        {
            Kind = UsbNodeKind.PnpDevice,
            Name = name,
            InstanceId = pnp.InstanceId ?? CmDevice.GetDeviceId(devInst),
            DevInst = devInst,
            Pnp = pnp,
        };

        if (isTunnelledUsbController)
        {
            node.Warnings.Add(
                "This is a tunnelled xHCI USB host controller; the USB devices behind it are also "
                + "listed under it in the host-controller tree (related by ContainerId).");
        }

        return node;
    }

    private static bool LooksLikeUsbHostController(PnpDeviceProperties pnp)
    {
        if (Contains(pnp.Service, "xhci") || Contains(pnp.Service, "usbxhci"))
        {
            return true;
        }

        bool usbClass = Contains(pnp.Class, "usb") || Contains(pnp.ClassGuid, "usb");
        return usbClass
               && (Contains(pnp.DeviceDescription, "host controller")
                   || Contains(pnp.BusReportedDeviceDesc, "host controller"));
    }

    private static bool Contains(string? value, string token)
        => value is not null && value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
