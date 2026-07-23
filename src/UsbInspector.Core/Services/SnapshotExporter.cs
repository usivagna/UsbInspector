using System.Text.Json;
using System.Text.Json.Serialization;
using UsbInspector.Core.Models;

namespace UsbInspector.Core.Services;

/// <summary>Serializes a USB snapshot to JSON for diagnostics/sharing.</summary>
public static class SnapshotExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new ByteArrayHexConverter() },
    };

    public static string ToJson(UsbSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Options);

    public static void Save(UsbSnapshot snapshot, string path) => File.WriteAllText(path, ToJson(snapshot));

    /// <summary>Writes raw descriptor bytes as an uppercase hex string rather than base64.</summary>
    private sealed class ByteArrayHexConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => Convert.FromHexString(reader.GetString() ?? string.Empty);

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
            => writer.WriteStringValue(Convert.ToHexString(value));
    }
}
