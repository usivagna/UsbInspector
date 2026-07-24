namespace UsbInspector.Core.Services;

/// <summary>
/// A single USB4 connection-manager TraceLogging rundown event, captured as its event name plus a
/// case-insensitive map of every payload field (as strings) so that both documented and undocumented
/// fields are preserved. Typed helpers extract the well-known fields.
/// </summary>
public sealed class Usb4Event
{
    public string EventName { get; init; } = string.Empty;

    /// <summary>Every payload field, keyed by name, formatted as a string.</summary>
    public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    public uint? DomainId { get; set; }

    /// <summary>Raw 7-byte topology ID, where present.</summary>
    public byte[]? TopologyId { get; set; }

    public string? DeviceInstancePath { get; set; }

    public string? Get(string name) => Fields.TryGetValue(name, out string? v) ? v : null;

    public bool GetBool(string name) =>
        string.Equals(Get(name), "True", StringComparison.OrdinalIgnoreCase) || Get(name) == "1";
}

/// <summary>The parsed result of a USB4 connection-manager ETW rundown.</summary>
public sealed class Usb4RundownData
{
    public List<Usb4Event> Events { get; } = new();

    /// <summary>True when the rundown could not run because the process is not elevated.</summary>
    public bool RequiresElevation { get; set; }

    public bool HasData => Events.Count > 0;

    public IEnumerable<Usb4Event> OfType(string eventName) =>
        Events.Where(e => string.Equals(e.EventName, eventName, StringComparison.OrdinalIgnoreCase));
}
