using UsbInspector.Core.Interop;
using UsbInspector.Core.Models;

namespace UsbInspector.Core.Services;

/// <summary>Top-level entry point: builds a complete USB topology snapshot for the system.</summary>
public sealed class UsbInspectorService
{
    /// <summary>Scans all host controllers and returns a full point-in-time USB snapshot.</summary>
    public UsbSnapshot Capture()
    {
        var snapshot = new UsbSnapshot
        {
            IsElevated = Elevation.IsProcessElevated(),
        };

        try
        {
            PnpDeviceIndex pnpIndex = PnpDeviceIndex.Build();
            var enumerator = new HostControllerEnumerator(pnpIndex);
            snapshot.HostControllers.AddRange(enumerator.Enumerate());
            snapshot.Usb4HostRouters.AddRange(new Usb4Enumerator().Enumerate(snapshot.Warnings));
            snapshot.PowerDelivery = PowerDeliveryService.Read();

            if (snapshot.HostControllers.Count == 0)
            {
                snapshot.Warnings.Add("No USB host controllers were found on this system.");
            }

            if (snapshot.Usb4HostRouters.Count == 0)
            {
                snapshot.Warnings.Add("No USB4 host routers were found (the system may not have USB4 hardware).");
            }

            if (!snapshot.IsElevated)
            {
                snapshot.Warnings.Add(
                    "Running without administrator rights. Some device/configuration descriptors may be unavailable; " +
                    "relaunch as Administrator for complete data.");
            }
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add($"USB scan failed: {ex.Message}");
        }

        new StorageInfoEnricher().Enrich(snapshot);
        return snapshot;
    }
}
