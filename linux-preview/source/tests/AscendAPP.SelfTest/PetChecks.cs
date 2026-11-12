using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Pets;
using AscendAPP.Core.Protocol;
using AscendAPP.Core.Storage;
using K4os.Compression.LZ4;
static class PetChecks
{
    private static readonly DateTimeOffset At=DateTimeOffset.Parse("2026-11-02T12:00:00Z");
    static byte[] U(uint n){var b=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(b,n);return b;}
    static byte[] V(uint n){var b=new List<byte>();do{byte v=(byte)(n&127);n>>=7;b.Add((byte)(v|(n>0?128:0)));}while(n>0);return b.ToArray();}
    static byte[] J(params byte[][] b)=>b.SelectMany(x=>x).ToArray();
    static byte[] State(uint points=13)=>J(U(0),V(1),U(1006),U(1006),U(1),V(1),U(1006),U(points),[9,8,7]); // documented extension tail
    static byte[] Gain(uint n=1)=>J([1],U(1006),U(n));
    static AionFrame Char(string name="FixturePet",uint id=17){var n=Encoding.UTF8.GetBytes(name);return new(0x3336,J(V(id),[0,0,(byte)n.Length],n,[0x18,5],U(13),[1],U(50)),false);}
    static byte[] Wire(AionFrame f)=>J(V((uint)f.Payload.Length+6),[(byte)(f.Opcode>>8),(byte)f.Opcode],f.Payload);
    static byte[] Packet(byte[] payload,uint seq){var b=new byte[54+payload.Length];BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(12),0x0800);b[14]=0x45;BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(16),(ushort)(40+payload.Length));b[23]=6;b[26]=10;b[29]=1;b[30]=10;b[33]=2;BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(34),13328);BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(36),50001);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(38),seq);b[46]=0x50;b[47]=0x18;payload.CopyTo(b,54);return b;}
    public static void Run(Action<bool,string> check)
    {
        check(PetProtocol.TryState(State(),out var state)&&state.Single()==new PetCount(1006,1,13),"Pet full-state prefix decodes current-level souls and preserves documented extension tail");
        check(!PetProtocol.TryState(State(25),out _),"Inconsistent pet points are rejected instead of inventing a level-up");
        var duplicate=J(U(0),V(2),U(1006),U(1006),U(1),U(1006),U(1006),U(1),V(0));
        check(!PetProtocol.TryState(duplicate,out _),"Duplicate pet state records are rejected");
        check(!PetProtocol.TryState(J(U(0),V(1025)),out _),"Oversized pet counts are bounded before allocation");
        check(!PetProtocol.TryState(J(U(0),V(1),U(1),U(2),U(1),V(0)),out _),"Mismatched duplicated pet IDs are rejected");
        check(PetProtocol.TryGain(Gain(),out var id,out int amount)&&id==1006&&amount==1,"Soul gain reads pet ID and actual amount");
        check(!PetProtocol.TryGain(J([2],U(1006),U(1)),out _,out _)&&!PetProtocol.TryGain(Gain(0),out _,out _)&&!PetProtocol.TryGain(J(Gain(),[0]),out _,out _),"Unknown gain kinds, zero gains and changed layouts do not alter counts");
        var random=new Random(422);for(int i=0;i<500;i++){var garbage=new byte[random.Next(0,160)];random.NextBytes(garbage);PetProtocol.TryState(garbage,out _);PetProtocol.TryGain(garbage,out _,out _);}
        check(true,"Malformed pet prefixes remain bounded and do not throw");
        using var tracker=new PetProgressStore(null,new Dictionary<uint,string>{{1006,"Sparkie"},{1097,"Karnif"}});
        tracker.Bind("A",1304);tracker.Observe(new PetGainObservation("A",1304,1006,1,At));
        check(!tracker.Trusted&&tracker.Rows(new HashSet<uint>(),false,false).Single(r=>r.Id==1006).Level is null&&tracker.Toast(At).Contains("total unknown"),"Pickups before synchronization show only actual gains, without guessed totals");
        tracker.Observe(new PetCollectionObservation("A",1304,[new(1006,1,13)],At));
        tracker.Observe(new PetGainObservation("A",1304,1006,1,At.AddSeconds(1)));
        var row=tracker.Rows(new HashSet<uint>{1006},true,true).Single();
        check(row.Level==1&&row.Points==14&&row.Progress=="14/25"&&row.Left=="11 souls to next level"&&row.Confirmed,"Observed gains update the next-level counter");
        check(tracker.Rows(new HashSet<uint>(),false,false).Single(r=>r.Id==1097).Level==0,"A full collection establishes absent catalogue pets at zero");
        tracker.Observe(new PetGainObservation("A",1304,1006,11,At.AddSeconds(2)));
        row=tracker.Rows(new HashSet<uint>{1006},true,false).Single();
        check(row.Level==2&&row.Points==0,"Pet rank transitions consume the current threshold exactly once");
        tracker.Observe(new PetGainObservation("A",1304,1006,100,At.AddSeconds(3)));
        row=tracker.Rows(new HashSet<uint>{1006},true,false).Single();
        check(row.Complete&&row.Progress=="MAX"&&row.Points==0,"Multi-rank gains cap at max without fabricated overflow progress");
        check(tracker.Rows(new HashSet<uint>{1006},true,true,now:At.AddSeconds(4)).Count==1&&tracker.Rows(new HashSet<uint>{1006},true,true,now:At.AddSeconds(20)).Count==0,"A freshly maxed pet is briefly visible, then respects the completed filter");
        tracker.Invalidate("gap");tracker.Observe(new PetGainObservation("A",1304,1097,3,At.AddSeconds(4)));
        check(!tracker.Trusted&&tracker.Rows(new HashSet<uint>{1097},true,false).Single().Points==0&&tracker.Toast(At.AddSeconds(4)).Contains("total unknown"),"Capture gaps freeze totals until a new authoritative collection");
        check(tracker.Toast(At.AddSeconds(12))=="","Pickup notifications expire without polling or retaining a live animation");
        tracker.Bind("B",1304);
        check(tracker.Rows(new HashSet<uint>{1006},true,false).Single().Level is null,"Character switching cannot borrow another character's collection");
        tracker.Bind("A",1304);
        check(!tracker.Trusted&&tracker.Rows(new HashSet<uint>{1006},true,false).Single().Complete,"Returning characters retain labelled cached progress");
        check(tracker.Name(99999)=="Pet #99999"&&tracker.Rows(new HashSet<uint>(),false,false,"KAR").Single().Name=="Karnif","English name search and unknown-ID fallback work offline");

        var d=new ExperimentalGlobalDecoder();var fulls=new List<PetCollectionObservation>();var gains=new List<PetGainObservation>();
        d.PetCollectionUpdated+=fulls.Add;d.PetSoulsReceived+=gains.Add;
        d.Decode("game",new(PetProtocol.StateOpcode,State(),true),At);
        check(fulls.Count==0&&!d.HasGameStream,"A pet frame alone cannot qualify an arbitrary TCP stream or invent its owner");
        d.Decode("game",Char(),At.AddSeconds(1));
        check(fulls.Count==1&&fulls[0].Character=="FixturePet"&&fulls[0].ServerId==1304,"A collection arriving before identity is bound to its own verified game session");
        d.Decode("game",new(PetProtocol.GainOpcode,Gain(),false),At.AddSeconds(2));
        d.Decode("other",new(PetProtocol.GainOpcode,Gain(),false),At.AddSeconds(2));
        check(gains.Count==1&&gains[0].Character=="FixturePet","Unqualified independent connections cannot emit pet gains");
        var deferred=new ExperimentalGlobalDecoder();using var deferredStore=new PetProgressStore(null);
        deferred.PetCollectionUpdated+=s=>deferredStore.Observe(s);deferred.PetSoulsReceived+=g=>deferredStore.Observe(g);
        deferred.Decode("deferred",new(PetProtocol.StateOpcode,State(),false),At);
        deferred.Decode("deferred",new(PetProtocol.GainOpcode,Gain(),false),At);
        deferred.Decode("deferred",Char(),At);
        check(deferredStore.Trusted&&deferredStore.Rows(new HashSet<uint>{1006},true,false).Single().Points==14,"Soul gains arriving between the collection and character identity are applied once after binding");
        var gap=new ExperimentalGlobalDecoder();int gapStates=0;gap.PetCollectionUpdated+=_=>gapStates++;
        gap.Decode("gap",new(PetProtocol.StateOpcode,State(),false),At);gap.InvalidateContinuity("gap",At);gap.Decode("gap",Char(),At);
        check(gapStates==0,"A capture gap discards an unbound collection instead of publishing stale totals");
        int invalidated=0;d.PetSyncLost+=_=>invalidated++;
        d.Decode("game",new(PetProtocol.GainOpcode,[2,0],false),At.AddSeconds(2));
        d.InvalidateContinuity("game",At.AddSeconds(3));
        check(d.PetRejected==1&&invalidated==2,"Invalid pet layouts and capture continuity loss explicitly invalidate trust");

        using var endToEnd=new PetProgressStore(null);
        var decoder=new ExperimentalGlobalDecoder();decoder.PetCollectionUpdated+=s=>endToEnd.Observe(s);decoder.PetSoulsReceived+=g=>endToEnd.Observe(g);
        var pipeline=new NetworkPipeline(decoder,_=>throw new Exception("Pet frames must not become combat"));
        var inner=J(Wire(new(PetProtocol.StateOpcode,State(),false)),Wire(new(PetProtocol.GainOpcode,Gain(),false)));
        var packed=new byte[LZ4Codec.MaximumOutputSize(inner.Length)];int bytes=LZ4Codec.Encode(inner,packed);var bundle=Wire(new(0xffff,J(U((uint)inner.Length),packed[..bytes]),false));
        var raw=J(Wire(Char()),bundle);var a=Packet(raw[..17],100);var b=Packet(raw[17..],117);
        pipeline.Ingest(1,a,"adapter-a",At);pipeline.Ingest(1,b,"adapter-a",At);pipeline.Ingest(1,b,"adapter-a",At);pipeline.Ingest(1,Packet(raw,100),"adapter-b",At);
        check(endToEnd.Trusted&&endToEnd.Rows(new HashSet<uint>{1006},true,false).Single().Points==14&&decoder.PetGains==1,"TCP fragments, LZ4 pet bundles, retransmissions and duplicate adapters yield one correctly owned soul gain");
        check(pipeline.Decoded==0,"Pet collection capture does not alter combat event totals or DPS filtering");

        var ordered=new ExperimentalGlobalDecoder();using var orderedStore=new PetProgressStore(null);
        ordered.PetCollectionUpdated+=s=>orderedStore.Observe(s);ordered.PetSoulsReceived+=g=>orderedStore.Observe(g);
        var tickets=new AionFrame(0x0b61,J([1,4],U(OdyleEnergyReader.TicketId),V(75)),false);
        ordered.Decode("ordered",Char(),At);ordered.Decode("ordered",tickets,At);ordered.Decode("ordered",new(PetProtocol.StateOpcode,State(),true),At);
        ordered.Decode("ordered",new(PetProtocol.GainOpcode,Gain(),false),At.AddSeconds(1));
        check(orderedStore.Trusted&&orderedStore.Rows(new HashSet<uint>{1006},true,false).Single().Points==14,"Own record followed by tickets in one captured login bundle no longer strands the pet collection");
        check(ordered.PetStateFrames==1&&ordered.PetStates==1&&ordered.PetGainFrames==1&&ordered.PetGains==1,"Raw pet-frame diagnostics distinguish observed messages from published totals");
        ordered.Decode("ordered",tickets,At.AddSeconds(5));ordered.Decode("ordered",new(PetProtocol.StateOpcode,State(),false),At.AddSeconds(5));
        check(ordered.PetStates==1,"A later login marker cannot assign a new collection to an earlier character");
        ordered.Decode("ordered",Char("OtherFixture",18),At.AddSeconds(6));
        check(ordered.PetStates==2&&orderedStore.Status.Contains("OtherFixture"),"Deferred collection binds after the new character's verified own record");
        ordered.Decode("ordered",new(PetProtocol.StateOpcode,State(25),false),At.AddSeconds(7));
        check(ordered.PetStateFrames==3&&ordered.PetRejected==1&&ordered.PetStateResult.Contains("Inconsistent progress"),"Pet diagnostics explain rejected fields without exporting raw packet bodies");
        var pickup=orderedStore.Pickup;
        check(pickup is null,"Character changes clear the previous character's pickup notification");
        string folder=Path.Combine(AppContext.BaseDirectory,"pet-fixtures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"pets.json");
        try{
            using(var first=new PetProgressStore(file)){first.Observe(new PetCollectionObservation("A",1304,[new(1006,1,14)],At));check(first.Save(),"Pet collections save atomically outside native capture callbacks");using var second=new PetProgressStore(file);second.Observe(new PetCollectionObservation("B",1304,[],At));check(!second.Save()&&second.LastError is not null,"A second instance cannot overwrite the pet archive");}
            using(var next=new PetProgressStore(file)){next.Bind("A",1304);var r=next.Rows(new HashSet<uint>{1006},true,false).Single();check(r.Points==14&&!r.Confirmed,"Pet progress survives restarts as cached rather than falsely live");}
            File.WriteAllText(file,"""{"Version":9,"Characters":[]}""");
            using(var future=new PetProgressStore(file)){future.Observe(new PetCollectionObservation("A",1304,[],At));check(!future.Save()&&File.ReadAllText(file).Contains(":9"),"Unknown pet archive formats are preserved without overwrite");}
            File.WriteAllText(file,"{broken");
            using(var corrupt=new PetProgressStore(file))check(corrupt.LastError is not null&&!corrupt.Trusted,"Damaged pet archives cannot crash startup or become confirmed data");
        }finally{string full=Path.GetFullPath(folder),fixture=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"pet-fixtures"));if(!full.StartsWith(fixture+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected fixture path");Directory.Delete(full,true);}
    }
}
