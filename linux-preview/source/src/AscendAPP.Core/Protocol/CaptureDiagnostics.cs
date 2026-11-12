namespace AscendAPP.Core.Protocol;

/// <summary>Session counters only; contains no endpoints, names or packet bodies.</summary>
public sealed record CaptureDiagnostics(long Packets, long Segments, long PayloadBytes,
    long Frames, long Decoded, long UnknownActors, bool SelfKnown, long Errors,
    long Gaps, long SkippedBytes, long MalformedBundles,DateTimeOffset? LastPayloadAt=null)
{
    public string Stage => Packets == 0 ? "No packets"
        : Segments == 0 ? "Packets without TCP payloads"
        : Frames == 0 ? "Packets without recognized frames"
        : Decoded == 0 ? "Frames without decoded events"
        : !SelfKnown || UnknownActors > 0 ? "Events with unknown player"
        : "Decoded events; player identity available";

    public string Summary => $"{Stage} | packets {Packets}, TCP {Segments}, candidate frames {Frames}, events {Decoded}, unknown actors {UnknownActors}, errors {Errors}, gaps {Gaps}, skipped {SkippedBytes}, bad bundles {MalformedBundles}";
}
