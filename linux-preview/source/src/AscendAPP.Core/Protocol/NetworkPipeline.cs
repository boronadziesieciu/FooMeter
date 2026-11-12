using AscendAPP.Core.Combat;
using AscendAPP.Core.Network;

namespace AscendAPP.Core.Protocol;

public sealed record LiveDecoderDiagnostics(bool GameStream, string? SelfName, long Recognized, long Rejected, IReadOnlyDictionary<string,long> Opcodes, OdyleEnergy? Energy = null,int? ServerId=null,IReadOnlyList<TicketBalance>? Tickets=null,string? ClassName=null,int? ZoneId=null,bool IdentityRefreshPending=false,long ZoneRefreshes=0,long CharacterRecords=0,string IdentityState="Waiting for own character record",long FieldBossLists=0,long UnboundFieldBossLists=0,long InternalMoves=0,long RetainedParties=0,long ProfileRebindings=0,int PartyMembers=0);

/// <summary>Serializes native callbacks, isolates capture surfaces and bounds streams.</summary>
public sealed class NetworkPipeline(ICombatPacketDecoder decoder, Action<CombatEvent> onCombat)
{
    private readonly object _sync = new();
    private readonly TcpReassembler _tcp = new();
    private readonly Dictionary<string, AionFrameReader> _readers = new();
    private readonly Dictionary<string, DateTimeOffset> _lastSeen = new();
    private long _segments, _frames, _decoded, _bytes, _packets, _errors, _unknownActors, _gaps, _skipped, _malformed;
    private bool _selfKnown, _wired;
    private DateTimeOffset _receivedAt;private DateTimeOffset? _lastPayloadAt;
    public event Action<TcpSegment,DateTimeOffset>? SegmentObserved;
    public event Action? CaptureReset;
    public LiveDecoderDiagnostics? LiveDiagnostics
    {
        get { lock (_sync) { if(decoder is not ExperimentalGlobalDecoder d)return null;d.ExpirePendingWorld(DateTimeOffset.UtcNow);return new(d.HasGameStream, d.SelfName, d.Recognized, d.Rejected, d.OpcodeCounts, d.Energy,d.SelfServerId,d.Tickets,d.SelfClass,d.ZoneId,d.IdentityRefreshPending,d.ZoneRefreshes,d.CharacterRecords,d.IdentityState,d.FieldBossLists,d.UnboundFieldBossLists,d.InternalMoves,d.RetainedParties,d.ProfileRebindings,d.PartyMembers); } }
    }
    public CaptureDiagnostics Diagnostics
    {
        get
        {
            lock (_sync) return new(_packets, _segments, _bytes, _frames, _decoded,
                _unknownActors, decoder is ExperimentalGlobalDecoder d ? d.SelfName is not null : _selfKnown, _errors, _gaps,
                _skipped + _readers.Values.Sum(r => r.SkippedBytes), _malformed + _readers.Values.Sum(r => r.MalformedBundles),_lastPayloadAt);
        }
    }
    public void RecordCaptureError() { lock (_sync) _errors++; }
    public void StartSession()
    {
        lock (_sync)
        {
            Initialize();
            ResetStreams();
            (decoder as IStreamCombatDecoder)?.Reset();
            _segments = _frames = _decoded = _bytes = _packets = _errors = _unknownActors = _gaps = _skipped = _malformed = 0;
            _selfKnown = false;_lastPayloadAt=null;
        }
    }
    public long Segments => Interlocked.Read(ref _segments);
    public long Frames => Interlocked.Read(ref _frames);
    public long Decoded => Interlocked.Read(ref _decoded);
    public long PayloadBytes => Interlocked.Read(ref _bytes);
    public string DecoderName => decoder.Name;
    public void Ingest(int linkType, byte[] packet, string captureSource = "", DateTimeOffset? receivedAt = null)
    {
        lock (_sync)
        {
            Initialize();
            _packets++;
            if (!TcpPacketExtractor.TryExtract(linkType, packet, out var parsed) || parsed is null) return;
            var tcp = parsed with { CaptureSource = captureSource };
            _receivedAt = receivedAt ?? DateTimeOffset.UtcNow;if(tcp.Payload.Length>0)_lastPayloadAt=_receivedAt;SegmentObserved?.Invoke(tcp,_receivedAt);
            // Outgoing world traffic is encrypted and has no server-event decoding path.
            // Searching it for plaintext S2C opcodes created spurious frames/gaps and wasted work.
            if(decoder is ExperimentalGlobalDecoder&&tcp.DestinationPort==13328&&tcp.SourcePort!=13328)
            {if(tcp.Payload.Length>0){_segments++;_bytes+=tcp.Payload.Length;}return;}
            if ((tcp.Flags & 2) != 0)
            {
                Forget(tcp.StreamId);
                tcp = tcp with { SequenceNumber = unchecked(tcp.SequenceNumber + 1) };
            }
            // Keep SYN-only streams under the same bound as payload streams. Seeding
            // before the first payload preserves its bytes when callbacks arrive out of order.
            if ((tcp.Flags & 2) != 0 || tcp.Payload.Length > 0)
            {
                if (!_readers.ContainsKey(tcp.StreamId))
                {
                    if (_readers.Count >= 64)
                        Forget(_lastSeen.MinBy(x => x.Value).Key);
                    string key = tcp.StreamId;
                    var reader = new AionFrameReader(decoder is IStreamCombatDecoder ? GlobalProtocolFacts.SyncOpcodes : null);
                    string source=tcp.CaptureSource;
                    string connection=$"{tcp.SourceAddress}:{tcp.SourcePort}>{tcp.DestinationAddress}:{tcp.DestinationPort}";
                    ushort sourcePort=tcp.SourcePort;
                    reader.FrameReady += frame => OnFrame(key, frame,source,connection,sourcePort);
                    _readers[key] = reader;
                }
                _lastSeen[tcp.StreamId] = _receivedAt;
            }
            if ((tcp.Flags & 2) != 0) _tcp.Seed(tcp.StreamId, tcp.SequenceNumber);
            if (tcp.Payload.Length > 0)
            {
                _segments++; _bytes += tcp.Payload.Length;
                _tcp.Push(tcp, _receivedAt);
            }
            if ((tcp.Flags & 5) != 0) Forget(tcp.StreamId);
        }
    }
    private void OnFrame(string streamId, AionFrame frame,string captureSurface,string connectionId,ushort sourcePort)
    {
        _frames++;
        IReadOnlyList<CombatEvent> events;
        try
        {
            if(decoder is ExperimentalGlobalDecoder global)events=global.Decode(streamId,frame,_receivedAt,captureSurface,connectionId,sourcePort);
            else if (decoder is IStreamCombatDecoder advanced) events = advanced.Decode(streamId, frame, _receivedAt);
            else if (decoder.TryDecode(frame, _receivedAt, out var e) && e is not null) events = [e];
            else return;
        }
        catch { _errors++; return; }
        foreach (var evt in events)
        {
            _decoded++;
            if (evt.ActorId == 0 || string.IsNullOrWhiteSpace(evt.ActorName) || evt.ActorName.StartsWith('#')) _unknownActors++;
            if (evt.IsSelf && evt.ActorId != 0) _selfKnown = true;
            onCombat(evt);
        }
    }
    private void Forget(string key)
    {
        if (_readers.Remove(key, out var old)) { _skipped += old.SkippedBytes; _malformed += old.MalformedBundles; }
        _lastSeen.Remove(key); _tcp.Forget(key);
        (decoder as IStreamCombatDecoder)?.ForgetStream(key);
    }
    public void Initialize()
    {
        lock (_sync)
        {
            if (_wired) return;
            _wired = true;
            _tcp.ChunkReady += (key, chunk) => { if (_readers.TryGetValue(key, out var framer)) framer.Feed(chunk); };
            _tcp.StreamGap += key => { _gaps++; if(decoder is ExperimentalGlobalDecoder global)global.InvalidateContinuity(key,_receivedAt); if (_readers.TryGetValue(key, out var framer)) framer.Reset(); };
        }
    }
    public void ResetStreams()
    {
        lock (_sync)
        {
            foreach (var key in _readers.Keys.ToArray()) Forget(key);
            _tcp.Reset(); _readers.Clear(); _lastSeen.Clear();CaptureReset?.Invoke();
        }
    }
}
