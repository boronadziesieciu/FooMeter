using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Network;

static class GlobalDecoderChecks
{
    // Hand-built wire fixtures derived from documented layouts, not live Global captures.
    static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-22T12:00:00Z");
    static byte[] V(uint value)
    {
        var b = new List<byte>();
        do { byte x = (byte)(value & 127); value >>= 7; b.Add((byte)(x | (value != 0 ? 128 : 0))); } while (value != 0);
        return b.ToArray();
    }
    static byte[] U32(uint n) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, n); return b; }
    static byte[] U64(ulong n) { var b = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, n); return b; }
    static byte[] Join(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
    static byte[] N(string name) { var b = Encoding.UTF8.GetBytes(name); return Join([(byte)b.Length], b); }
    static AionFrame F(ushort opcode, byte[] payload) => new(opcode, payload, false);
    static AionFrame Character(uint id = 17, string name = "FixtureHero", uint code = 13, byte faction = 1) =>
        F(0x3336, Join(V(id), [0,0], N(name), [0x18,5], U32(code), [faction], U32(50)));
    static AionFrame Hit(uint actor = 17, uint target = 900, uint skill = 14010000, uint amount = 12345, bool critical = false, byte count = 1, byte flags = 4) =>
        F(0x0438, Join(V(target), [flags,0], V(actor), U32(skill), [0,(byte)(critical ? 3 : 0)], [count,0,0,0], V(20000), V(amount)));
    static AionFrame Tick(uint actor = 17, uint target = 900, byte flags = 10, uint effect = 1401000011, uint amount = 250, uint healed = 0, uint skill = 14010000) =>
        F(0x0538, Join(V(target), [flags], V(actor), V(1), U32(effect),
            (flags & 2) != 0 ? V(amount) : [], (flags & 1) != 0 ? V(healed) : [], U32(skill)));
    static byte[] Wire(AionFrame f) => Join(V((uint)f.Payload.Length + 6), [(byte)(f.Opcode >> 8), (byte)f.Opcode], f.Payload);
    static byte[] Packet(byte[] payload, uint seq, byte flags = 0x18)
    {
        var b = new byte[54 + payload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(12), 0x0800);
        b[14] = 0x45; BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(16), (ushort)(40 + payload.Length));
        b[23] = 6; b[26] = 10; b[29] = 1; b[30] = 10; b[33] = 2;
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(34), 13328);
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(36), 50001);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(38), seq);
        b[46] = 0x50; b[47] = flags;
        payload.CopyTo(b, 54); return b;
    }
    public static void Run(Action<bool,string> check)
    {
        var d = new ExperimentalGlobalDecoder();
        check(d.Decode("other", Hit(), At).Count == 0 && !d.HasGameStream, "Unqualified TCP cannot emit guessed combat");
        var identities = new List<PlayerIdentity>(); d.IdentityUpdated += identities.Add;
        check(d.Decode("game", Character(), At).Count == 0 && d.SelfName == "FixtureHero", "Login record establishes self without damage");
        var e = d.Decode("game", Hit(critical:true, count:3), At).Single();
        check(e is { ActorId:17, ActorName:"FixtureHero", Amount:12345, IsSelf:true, HitCount:3, ActorClass:"Ranger", Kind:CombatKind.Damage } &&
            e.Tags.HasFlag(HitTags.Critical), "Direct-hit varint amount, identity, class, critical and multi-hit count");
        var engine = new CombatEngine(); engine.Add(e);
        var snap = engine.Snapshot(At);
        check(snap.TotalDamage == 12345 && snap.Players[0].Hits == 3 && snap.Players[0].CritRate == 1,
            "Multi-hit count never multiplies wire damage");
        check(d.Decode("game", Hit(flags:0), At).Count == 0, "Cast companion notice has no damage");
        check(d.Decode("game", Hit(skill:11000100), At).Count == 0, "Dodge marker excluded");
        check(d.Decode("game", Hit(skill:11780000), At).Count == 0, "Known buff family excluded");
        check(d.Decode("game", Hit(amount:100000001), At).Count == 0, "Implausible amount rejected");
        check(d.Decode("game", Hit(skill:17240000, target:17), At).Single().Kind == CombatKind.Heal,
            "Direct healing family goes to healing");
        check(d.Decode("game", Tick(), At).Single() is { Amount:250, Kind:CombatKind.Damage, Tags:HitTags.Dot },
            "DOT uses actual tick amount");
        check(d.Decode("game", Tick(target:17,flags:11,amount:251,healed:83,skill:17240000), At).Single() is { Amount:83, Kind:CombatKind.Heal },
            "Heal-over-time uses heal field rather than remaining total");
        check(d.Decode("game", Tick(effect:160466011), At).Count == 0, "Monster-triggered effect never added as player DOT");
        check(d.Decode("game", F(0x0538, [1,10,2,1]), At).Count == 0, "Truncated DOT rejected");
        check(d.Decode("game", F(0x0438, [255,255,255,255,255,0]), At).Count == 0, "Overflowing entity varint rejected");
        d.Decode("game", F(0x048d, Join(V(900),U32(14010000),V(18),[0x17,5],N("PartyMate"))),At);
        check(d.Decode("game", Hit(actor:18), At).Single().ActorName == "PartyMate", "Appearance maps player independently of server");
        d.Decode("game", F(0x4536,Join(V(19),[0,0],N("OtherHero"),[0,0,0,0,1])),At);
        check(d.Decode("game", Hit(actor:19), At).Single().ActorName == "OtherHero", "Nickname maps another combat ID");
        check(d.Decode("duplicate-adapter", Character(), At).Count == 0 &&
              d.Decode("duplicate-adapter", Hit(), At).Count == 0, "Second capture surface cannot duplicate combat");
        var unknown = d.Decode("game", Hit(actor:20), At).Single();
        check(unknown.ActorName == "#20" && !unknown.IsSelf, "Unknown actor stays unknown and never becomes self");
        engine.Add(unknown); engine.UpdateIdentity(20,"LateName","Ranger",false);
        check(engine.Snapshot(At).Players.Any(p=>p.ActorId==20 && p.Name=="LateName"), "Late identity updates existing combat rows");

        byte[] spawn = Join(V(900),[0,0,0],U32(2400001),new byte[21],V(50000),V(100000),[100,0,0,0,100,0,0,0]);
        d.Decode("game",F(0x4136,spawn),At);
        e = d.Decode("game",Hit(),At).Single(); engine.Reset(At); engine.Add(e);
        check(e.TargetNpcCode == 2400001 && e.TargetMaxHp == 100000 && !e.TargetIsBoss,
            "Spawn provides target health without inventing boss identity");
        TargetHealth? health = null; d.HealthUpdated += h => { health=h; engine.UpdateHealth(h.Id,h.Current,h.Maximum); };
        d.Decode("game",F(0x008d,Join(V(900),[2,2,0],U64(45000),[7],U64(100000))),At);
        check(health is {Current:45000,Maximum:100000} && engine.Snapshot(At).TargetHealth?.CurrentHp==45000,
            "HP updates refresh target bar without a fabricated combat event");
        check(d.Decode("game", Hit(actor:900), At).Count==0, "Known NPC not ranked as a player");
        byte[] petSpawn = Join(V(600),[0,0,0],U32(100011),[255,255,255,255,255,255,255,255],new byte[8],V(17));
        d.Decode("game",F(0x4136,petSpawn),At);
        check(d.Decode("game",Hit(actor:600,skill:100011),At).Single() is { ActorId:17,IsSelf:true },
            "Explicit spawn owner receives summon damage");

        var labels=new ExperimentalGlobalDecoder();labels.Decode("labels",Character(),At);
        var firstLabel=labels.Decode("labels",Hit(actor:27),At).Single();
        var secondLabel=labels.Decode("labels",Hit(actor:27),At.AddMilliseconds(1)).Single();
        check(ReferenceEquals(firstLabel.SkillName,secondLabel.SkillName)&&ReferenceEquals(firstLabel.ActorName,secondLabel.ActorName),"Repeated wire skill and unknown-actor labels reuse strings");
        labels.Decode("labels",F(0x4536,Join(V(27),[0,0],N("ResolvedHero"))),At.AddMilliseconds(2));
        check(labels.Decode("labels",Hit(actor:27),At.AddMilliseconds(3)).Single() is {ActorName:"ResolvedHero",IsSelf:false},"Cached anonymous labels never become identity or mask a later nickname");
        labels.Decode("labels",F(0x4136,spawn),At.AddMilliseconds(4));
        check(ReferenceEquals(labels.Decode("labels",Hit(),At.AddMilliseconds(5)).Single().TargetName,
            labels.Decode("labels",Hit(),At.AddMilliseconds(6)).Single().TargetName),"Unknown NPC display labels are reused without changing classification");

        var clockDecoder = new ExperimentalGlobalDecoder();
        for(int i=0;i<3;i++) clockDecoder.Decode("clock",F(0x0036,U64((ulong)At.AddMilliseconds(i*50).ToUnixTimeMilliseconds())),At.AddMilliseconds(i*50));
        check(clockDecoder.HasGameStream && clockDecoder.SelfName is null, "Mid-session clocks identify game without guessing self");
        check(clockDecoder.Decode("clock",Hit(),At.AddSeconds(1)).Single().IsSelf==false, "Mid-session unknown self stays unresolved");
        clockDecoder.Reset(); check(!clockDecoder.HasGameStream && clockDecoder.Recognized==0, "Decoder reset removes stale identity and flow");
        var asmo = new ExperimentalGlobalDecoder(); asmo.Decode("game",Character(code:15,faction:2),At);
        check(asmo.SelfName=="FixtureHero", "Character reader accepts both faction codes");

        var events = new List<CombatEvent>();
        var live = new ExperimentalGlobalDecoder();
        var pipeline = new NetworkPipeline(live,events.Add); pipeline.StartSession();
        byte[] start=Wire(Character()), hit=Wire(Hit());
        pipeline.Ingest(1,Packet(start[..4],1000),"ethernet",At);
        pipeline.Ingest(1,Packet(start[4..],1004),"ethernet",At);
        pipeline.Ingest(1,Packet(hit,(uint)(1000+start.Length)),"ethernet",At);
        pipeline.Ingest(1,Packet(hit,(uint)(1000+start.Length)),"ethernet",At);
        pipeline.Ingest(1,Packet(start,1000),"loopback",At);
        pipeline.Ingest(1,Packet(hit,(uint)(1000+start.Length)),"loopback",At);
        check(events.Count==1 && events[0].Amount==12345 && pipeline.LiveDiagnostics?.SelfName=="FixtureHero",
            "Ethernet -> TCP -> split framing -> live decoder -> ingestion with retransmission and adapter dedup");
        pipeline.Ingest(1,Packet([],2000,2),"ethernet",At.AddSeconds(1));
        byte[] relog=Join(Wire(Character(21,"Relogged")),Wire(Hit(actor:21)));
        pipeline.Ingest(1,Packet(relog,2001),"ethernet",At.AddSeconds(1));
        check(events.Count==2 && events[^1] is { ActorId:21,IsSelf:true,ActorName:"Relogged" },
            "SYN reusing same endpoints starts a fresh character session");
        check(pipeline.Diagnostics.Errors==0,"Experimental fixture pipeline has no swallowed exceptions");

        var reader=new AionFrameReader(GlobalProtocolFacts.SyncOpcodes);
        var frames=new List<AionFrame>(); reader.FrameReady+=frames.Add;
        byte[] tls=[23,3,3,0,4,9,9,9,9];
        reader.Feed(Join(start,tls,hit));
        check(frames.Count==2 && reader.TlsRecords==1,"TLS ciphertext does not swallow following game frame");
        var resync=new AionFrameReader(GlobalProtocolFacts.SyncOpcodes);
        var synced=new List<AionFrame>(); resync.FrameReady+=synced.Add;
        resync.Feed(Join([255,255,255,255,127],start,hit));
        check(synced.Count==2 && resync.SkippedBytes==5,"Midstream framing resynchronizes at known opcode chain");
        var tcp=new TcpReassembler(); int gaps=0; var bytes=new List<byte>();
        tcp.StreamGap+=_=>gaps++; tcp.ChunkReady+=(_,b)=>bytes.AddRange(b);
        TcpSegment Seg(uint seq,params byte[] b)=>new("a",1,"b",2,seq,b);
        tcp.Push(Seg(100,1),At); tcp.Push(Seg(105,6),At); tcp.Push(Seg(106,7),At.AddSeconds(3));
        check(gaps==1 && bytes.SequenceEqual(new byte[]{1,7}),"Persistent TCP gap recovers without fabricating missing bytes");

        var compressedEvents=new List<CombatEvent>();
        var compressedPipeline=new NetworkPipeline(new ExperimentalGlobalDecoder(),compressedEvents.Add);
        byte[] plain=Join(start,hit);
        var encoded=new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(plain.Length)];
        int encodedCount=K4os.Compression.LZ4.LZ4Codec.Encode(plain,0,plain.Length,encoded,0,encoded.Length);
        var compressed=F(0xffff,Join(U32((uint)plain.Length),encoded[..encodedCount]));
        compressedPipeline.Ingest(1,Packet(Wire(compressed),3000),"bundle",At);
        check(compressedEvents.Count==1 && compressedEvents[0].IsSelf && compressedEvents[0].Amount==12345,
            "LZ4 bundle -> login identity -> live hit -> ingestion");
        var atomic=new AionFrameReader();
        var atomicFrames=new List<AionFrame>(); atomic.FrameReady+=atomicFrames.Add;
        byte[] malformedPlain=Join(hit,[127,1]);
        var encBad=new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(malformedPlain.Length)];
        int badCount=K4os.Compression.LZ4.LZ4Codec.Encode(malformedPlain,0,malformedPlain.Length,encBad,0,encBad.Length);
        atomic.Feed(Wire(F(0xffff,Join(U32((uint)malformedPlain.Length),encBad[..badCount]))));
        check(atomicFrames.Count==0 && atomic.MalformedBundles==1,"Malformed bundle cannot partially emit combat");
        check(pipeline.LiveDiagnostics?.Opcodes.ContainsKey("0438")==true,"Diagnostic opcode census contains no payload bytes");

        var rnd=new Random(123);
        long before=d.Rejected;
        for(int i=0;i<3000;i++)
        {
            var bad=new byte[rnd.Next(0,80)]; rnd.NextBytes(bad);
            d.Decode("game",F(new ushort[]{0x0438,0x0538,0x3336,0x4536,0x048d,0x4136,0x008d}[i%7],bad),At);
        }
        check(d.Rejected>before,"Malformed-payload stress run completes without exception");
    }
}
