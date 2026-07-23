using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;
using Windows.Win32;

namespace UsbInspector.Core.Services;

/// <summary>Enumerates USB host controllers and their root hubs, then walks each topology.</summary>
public sealed class HostControllerEnumerator
{
    private readonly PnpDeviceIndex _pnpIndex;
    private readonly HubTopologyWalker _walker;

    public HostControllerEnumerator(PnpDeviceIndex pnpIndex)
    {
        _pnpIndex = pnpIndex;
        _walker = new HubTopologyWalker(pnpIndex);
    }

    public IReadOnlyList<UsbNode> Enumerate()
    {
        var controllers = new List<UsbNode>();

        foreach (DeviceInterfaceInstance instance in DeviceInterfaceEnumerator.Enumerate(PInvoke.GUID_DEVINTERFACE_USB_HOST_CONTROLLER))
        {
            controllers.Add(BuildController(instance));
        }

        return controllers;
    }

    private UsbNode BuildController(DeviceInterfaceInstance instance)
    {
        PnpDeviceProperties pnp = PnpPropertyReader.Read(instance.DevInst);
        var controller = new UsbNode
        {
            Kind = UsbNodeKind.HostController,
            Name = pnp.BestDisplayName,
            InstanceId = instance.InstanceId,
            DevicePath = instance.DevicePath,
            DevInst = instance.DevInst,
            Pnp = pnp,
        };

        using SafeFileHandle handle = NativeDevice.Open(instance.DevicePath, writeAccess: true);
        if (handle.IsInvalid)
        {
            controller.Warnings.Add($"Could not open host controller (error {Marshal.GetLastWin32Error()}).");
            return controller;
        }

        string? rootHubName = HubQueries.GetRootHubName(handle);
        if (string.IsNullOrEmpty(rootHubName))
        {
            controller.Warnings.Add("Could not read root hub name for this controller.");
            return controller;
        }

        var rootHub = new UsbNode
        {
            Kind = UsbNodeKind.RootHub,
            Name = "Root Hub",
            IsHub = true,
        };
        controller.Children.Add(rootHub);

        _walker.WalkHub(rootHub, rootHubName);
        return controller;
    }
}
