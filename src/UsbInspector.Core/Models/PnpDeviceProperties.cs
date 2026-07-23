namespace UsbInspector.Core.Models;

/// <summary>
/// PnP / driver properties for a devnode, read via SetupAPI DEVPKEYs and CfgMgr32.
/// Well-known values are surfaced as typed members; every retrieved property is also
/// stored in <see cref="AllProperties"/> for the raw view.
/// </summary>
public sealed class PnpDeviceProperties
{
    public string? DeviceDescription { get; set; }
    public string? FriendlyName { get; set; }
    public string? BusReportedDeviceDesc { get; set; }
    public string? Manufacturer { get; set; }
    public string? Service { get; set; }
    public string? Class { get; set; }
    public string? ClassGuid { get; set; }
    public string? EnumeratorName { get; set; }

    public string? Driver { get; set; }
    public string? DriverVersion { get; set; }
    public string? DriverDate { get; set; }
    public string? DriverProvider { get; set; }

    public string? InstanceId { get; set; }
    public string? ContainerId { get; set; }
    public string? LocationInfo { get; set; }
    public string? PdoName { get; set; }
    public uint? Address { get; set; }
    public uint? BusNumber { get; set; }

    public string[] HardwareIds { get; set; } = Array.Empty<string>();
    public string[] CompatibleIds { get; set; } = Array.Empty<string>();
    public string[] LocationPaths { get; set; } = Array.Empty<string>();

    public bool? IsPresent { get; set; }
    public uint? ProblemCode { get; set; }
    public string? ProblemDescription { get; set; }
    public string? StatusFlags { get; set; }

    /// <summary>Every property key/value pair retrieved from the devnode, for the raw view.</summary>
    public Dictionary<string, string> AllProperties { get; } = new(StringComparer.Ordinal);

    public string BestDisplayName =>
        FriendlyName
        ?? BusReportedDeviceDesc
        ?? DeviceDescription
        ?? Manufacturer
        ?? InstanceId
        ?? "Unknown device";
}
