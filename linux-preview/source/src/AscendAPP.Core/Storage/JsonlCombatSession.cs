using System.Text.Json;
using AscendAPP.Core.Combat;

namespace AscendAPP.Core.Storage;

/// <summary>Explicit opt-in import/export of *normalized* events, never raw packets.</summary>
public static class JsonlCombatSession
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static IReadOnlyList<CombatEvent> Read(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length > 32 * 1024 * 1024)
            throw new InvalidDataException("File is missing or exceeds the 32 MB import limit.");
        var result = new List<CombatEvent>();
        int lineNo = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            CombatEvent item;
            try { item = JsonSerializer.Deserialize<CombatEvent>(line, Options)
                         ?? throw new InvalidDataException("Null event."); }
            catch (JsonException e) { throw new InvalidDataException($"Invalid JSON at line {lineNo}.", e); }
            if (item.EffectiveHealing is <0 || item.EffectiveHealing>item.Amount || item.HealingAssessment?.Length>200 || item.Amount <= 0 || item.ActorName is null || item.TargetName is null || item.ActorName.Length > 100 || item.TargetName.Length > 100)
                throw new InvalidDataException($"Invalid event at line {lineNo}.");
            result.Add(item);
            if (result.Count > 200_000) throw new InvalidDataException("Too many events.");
        }
        return result.OrderBy(e => e.Time).ToArray();
    }

    public static void Write(string path, IEnumerable<CombatEvent> events)
    {
        using var writer = new StreamWriter(path);
        foreach (var e in events) writer.WriteLine(JsonSerializer.Serialize(e, Options));
    }

    public static void SaveSnapshot(string path, EncounterSnapshot snapshot) =>
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
}
