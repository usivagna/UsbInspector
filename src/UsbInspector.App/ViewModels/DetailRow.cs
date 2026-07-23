namespace UsbInspector_App.ViewModels;

/// <summary>A single label/value pair shown in a details section.</summary>
public sealed class DetailRow
{
    public DetailRow(string label, string? value)
    {
        Label = label;
        Value = string.IsNullOrEmpty(value) ? "—" : value;
    }

    public string Label { get; }

    public string Value { get; }
}
