namespace AscendAPP.Core.Network;

/// <summary>
/// One TCP direction only. Removes retransmissions, keeps limited out-of-order packets,
/// and signals discontinuity rather than inventing missing bytes. Not a full TCP stack.
/// </summary>
public sealed class TcpReassembler
{
    private sealed class StreamState
    {
        public uint Expected;
        public bool Started;
        public readonly Dictionary<uint, byte[]> Pending = new();
        public int PendingBytes;
        public DateTimeOffset PendingSince;
    }

    private readonly Dictionary<string, StreamState> _streams = new();
    private const int MaxStreams = 64;
    private const int MaxPendingSegments = 16;
    private const int MaxPendingBytes = 256 * 1024;
    public event Action<string, byte[]>? ChunkReady;
    public event Action<string>? StreamGap;

    // When a SYN was captured, hold later segments until the first post-SYN byte arrives.
    public void Seed(string streamId,uint sequence){if(_streams.Count>=MaxStreams)_streams.Clear();_streams[streamId]=new StreamState{Started=true,Expected=sequence};}
    public bool HasPending(string streamId)=>_streams.TryGetValue(streamId,out var s)&&s.Pending.Count>0;
    public void Reset() => _streams.Clear();

    public void Forget(string streamId) => _streams.Remove(streamId);

    public void Push(TcpSegment segment, DateTimeOffset? receivedAt = null)
    {
        if (segment.Payload.Length == 0 || segment.Payload.Length > 65535) return;
        if (!_streams.TryGetValue(segment.StreamId, out var stream))
        {
            if (_streams.Count >= MaxStreams) _streams.Clear();
            stream = new StreamState();
            _streams[segment.StreamId] = stream;
        }

        if (!stream.Started)
        {
            stream.Started = true;
            stream.Expected = segment.SequenceNumber;
        }

        var now = receivedAt ?? DateTimeOffset.UtcNow;
        var delta = unchecked((int)(segment.SequenceNumber - stream.Expected));
        if (delta > 0)
        {
            if (stream.Pending.Count >= MaxPendingSegments || stream.PendingBytes + segment.Payload.Length > MaxPendingBytes ||
                stream.Pending.Count > 0 && now - stream.PendingSince > TimeSpan.FromSeconds(2))
            {
                StreamGap?.Invoke(segment.StreamId);
                stream.Pending.Clear();
                stream.PendingBytes = 0;
                stream.Expected = segment.SequenceNumber;
            }
            else
            {
                if (stream.Pending.Count == 0) stream.PendingSince = now;
                if (stream.Pending.TryAdd(segment.SequenceNumber, segment.Payload))
                    stream.PendingBytes += segment.Payload.Length;
                return;
            }
        }
        Accept(stream, segment.StreamId, segment.SequenceNumber, segment.Payload);

        // Out-of-order packets may arrive after a gap is filled.
        while (true)
        {
            uint? ready = null;
            foreach (var key in stream.Pending.Keys)
            {
                if (unchecked((int)(key - stream.Expected)) <= 0) { ready = key; break; }
            }
            if (ready is not { } next) break;
            var bytes = stream.Pending[next];
            stream.Pending.Remove(next);
            stream.PendingBytes -= bytes.Length;
            Accept(stream, segment.StreamId, next, bytes);
        }
    }

    private void Accept(StreamState stream, string key, uint sequence, byte[] data)
    {
        var overlap = unchecked((int)(stream.Expected - sequence));
        if (overlap < 0 || overlap >= data.Length) return;
        var novel = data.AsSpan(overlap).ToArray();
        stream.Expected = unchecked(stream.Expected + (uint)novel.Length);
        ChunkReady?.Invoke(key, novel);
    }
}
