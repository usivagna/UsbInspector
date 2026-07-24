namespace UsbInspector_App.ViewModels;

/// <summary>A single label/value pair shown in a details section.</summary>
public sealed class DetailRow
{
    public DetailRow(string label, string? value, string? hint = null)
    {
        Label = label;
        Value = string.IsNullOrEmpty(value) ? "—" : value;
        Hint = hint;
    }

    public string Label { get; }

    public string Value { get; }

    /// <summary>Optional plain-language explanation shown as a tooltip (info glyph appears when set).</summary>
    public string? Hint { get; }

    public bool HasHint => !string.IsNullOrEmpty(Hint);
}
