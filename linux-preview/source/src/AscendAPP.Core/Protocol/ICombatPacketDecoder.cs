using AscendAPP.Core.Combat;

namespace AscendAPP.Core.Protocol;

/// <summary>
/// Protocol version adapters go here. Refuse uncertain layouts instead of producing
/// fake combat results. Decoder implementations should be tested against consented,
/// sanitized live captures; do not log packet bodies by default.
/// </summary>
public interface ICombatPacketDecoder
{
    string Name { get; }
    bool TryDecode(AionFrame frame, DateTimeOffset receivedAt, out CombatEvent? combatEvent);
}

public sealed class UnverifiedGlobalDecoder : ICombatPacketDecoder
{
    public string Name => "Aion 2 Global (validation required)";

    public bool TryDecode(AionFrame frame, DateTimeOffset receivedAt, out CombatEvent? combatEvent)
    {
        // TODO: Verify real opcode-specific payload layouts, actors, targets, flags,
        // skill IDs and damage widths against known Global packet fixtures.
        // Even a matching opcode (e.g. damage family) does not prove offsets.
        combatEvent = null;
        return false;
    }
}
