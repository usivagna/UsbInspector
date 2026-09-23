using System.Text.Json;
using UsbInspector.Core.Models;

namespace UsbInspector.Core.Services;

/// <summary>Local, user-supplied port labels; no storage contents are persisted.</summary>
public sealed class PortLayoutStore(string path)
{
    public Dictionary<string, PortAssignment> Load()
    {
        if (!File.Exists(path))
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }

        var saved = JsonSerializer.Deserialize<Dictionary<string, PortAssignment?>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The port layout is empty.");
        var result = new Dictionary<string, PortAssignment>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, assignment) in saved)
        {
            if (assignment is null || !Enum.IsDefined(assignment.Location) || assignment.Label is null
                || assignment.Label.Length > 80)
            {
                throw new InvalidDataException("The port layout contains an invalid label or location.");
            }

            result[key] = assignment;
        }

        return result;
    }

    public void Save(IReadOnlyDictionary<string, PortAssignment> assignments)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(assignments));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
