using System.Text.Json;

namespace AscendAPP.Core.Game;

/// <summary>
/// Optional mapping of confirmed NPC codes to names. Does NOT claim to identify a
/// boss by its human-readable display name or by numeric heuristics.
/// </summary>
public sealed class BossNameCatalog
{
    private readonly Dictionary<int, string> _names;
    private BossNameCatalog(Dictionary<int, string> names) => _names = names;
    public string? Resolve(int? code) => code.HasValue && _names.TryGetValue(code.Value, out var name) ? name : null;

    public static BossNameCatalog Read(string path)
    {
        if (!File.Exists(path)) return new(new Dictionary<int, string>());
        if (new FileInfo(path).Length > 2_000_000) throw new InvalidDataException("Boss catalogue is too large.");
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new();
        var clean = new Dictionary<int, string>();
        foreach (var (key, value) in raw)
        {
            if (!int.TryParse(key, out int code) || code <= 0 || string.IsNullOrWhiteSpace(value) || value.Length > 100)
                throw new InvalidDataException("Invalid boss catalogue entry.");
            clean[code] = value.Trim();
        }
        return new(clean);
    }
}
