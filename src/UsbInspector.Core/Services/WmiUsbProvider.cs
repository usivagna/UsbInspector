using System.Management;

namespace UsbInspector.Core.Services;

/// <summary>A USB-related entry retrieved from WMI.</summary>
public sealed class WmiUsbEntry
{
    public string Source { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? DeviceId { get; init; }
    public string? PnpDeviceId { get; init; }
    public string? Manufacturer { get; init; }
    public string? Service { get; init; }
    public string? Status { get; init; }
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Supplementary USB data from WMI (Win32_USBController, Win32_USBHub, and USB PnP entities).
/// Used as a cross-check/complement to the SetupAPI + IOCTL topology walk.
/// </summary>
public static class WmiUsbProvider
{
    public static IReadOnlyList<WmiUsbEntry> Query()
    {
        var entries = new List<WmiUsbEntry>();
        entries.AddRange(QueryClass("Win32_USBController"));
        entries.AddRange(QueryClass("Win32_USBHub"));
        entries.AddRange(QueryUsbPnpEntities());
        return entries;
    }

    private static IEnumerable<WmiUsbEntry> QueryClass(string className)
    {
        var results = new List<WmiUsbEntry>();
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM {className}");
            foreach (ManagementBaseObject mo in searcher.Get())
            {
                results.Add(ToEntry(mo, className));
                mo.Dispose();
            }
        }
        catch
        {
            // WMI class may be unavailable; ignore and return what we have.
        }

        return results;
    }

    private static IEnumerable<WmiUsbEntry> QueryUsbPnpEntities()
    {
        var results = new List<WmiUsbEntry>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                "SELECT * FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'USB%'");
            foreach (ManagementBaseObject mo in searcher.Get())
            {
                results.Add(ToEntry(mo, "Win32_PnPEntity"));
                mo.Dispose();
            }
        }
        catch
        {
            // ignore
        }

        return results;
    }

    private static WmiUsbEntry ToEntry(ManagementBaseObject mo, string source)
    {
        var entry = new WmiUsbEntry
        {
            Source = source,
            Name = GetString(mo, "Name"),
            DeviceId = GetString(mo, "DeviceID"),
            PnpDeviceId = GetString(mo, "PNPDeviceID"),
            Manufacturer = GetString(mo, "Manufacturer"),
            Service = GetString(mo, "Service"),
            Status = GetString(mo, "Status"),
        };

        foreach (PropertyData prop in mo.Properties)
        {
            if (prop.Value is not null)
            {
                entry.Properties[prop.Name] = prop.Value is Array arr
                    ? string.Join(", ", arr.Cast<object>())
                    : prop.Value.ToString() ?? string.Empty;
            }
        }

        return entry;
    }

    private static string? GetString(ManagementBaseObject mo, string name)
    {
        try
        {
            return mo[name]?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
