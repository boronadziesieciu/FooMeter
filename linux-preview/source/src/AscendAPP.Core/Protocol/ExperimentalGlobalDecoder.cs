// Selected wire readers adapted from SkeeveAN/Aion-DPS-Meter:
// Client/Aion2/Protocol/Aion2FrameDecoder.cs, revision 53033afadf3395dd47708abb308346734d8b1a8a.
// Copyright (c) 2026 SkeeveTV. MIT; see THIRD_PARTY_NOTICES/SkeeveAN-MIT.txt.
// No estimated hits, nickname-only self guessing or guessed summon owners are included.
using System.Buffers.Binary;
using System.Text;
using AscendAPP.Core.Pets;
using AscendAPP.Core.Combat;
using AscendAPP.Core.Game;

namespace AscendAPP.Core.Protocol;

public sealed record PlayerIdentity(uint Id, string Name, string? ClassName, bool IsSelf, int? ServerId = null,int? Faction = null,bool PlayerVerified=true);
public sealed record TargetHealth(uint Id, long? Current, long? Maximum, DateTimeOffset? ObservedAt = null);

public interface IStreamCombatDecoder : ICombatPacketDecoder
{
    IReadOnlyList<CombatEvent> Decode(string streamId, AionFrame frame, DateTimeOffset at);
    void Reset();
    void ForgetStream(string streamId);
}

public sealed class ExperimentalGlobalDecoder(BossTimingCatalog? timing = null,TargetCatalog? targets=null) : IStreamCombatDecoder
{
    private sealed class Entity
    {
        public string? Name, ClassName, AnonymousName, NpcLabel;
        public uint? Owner;
        public int? Npc;
        public long? Hp, MaxHp;
        public int? ServerId,Faction;public bool PlayerVerified;
        public DateTimeOffset? HpAt;
    }
    private sealed class Session
    {
        public readonly Dictionary<uint, Entity> Entities = new();
        public uint? Self;
        public OdyleEnergy? Energy;
        public (IReadOnlyList<PetCount> Counts,DateTimeOffset At)? PendingPets;
        public readonly List<(uint Pet,int Amount,DateTimeOffset At)> PendingPetGains=new();
        public readonly Dictionary<uint,TicketBalance> Tickets=new();
        public PartyRoster? Party;public bool PartyScoresPending;
        public bool LoginPending, AwaitingCharacter, Retired, ProfileRebindAllowed;
        public DateTimeOffset? OwnRecordAt;public bool PetIdentityConfirmed;
        public string? CaptureSurface, ConnectionId, VerifiedName, VerifiedClass;
        public ushort? SourcePort;
        public int? VerifiedServer, VerifiedFaction, ZoneId;
        public uint? ZoneCounter;
        public int Clocks;
        public DateTimeOffset FirstClock, LastSeen;
        public long LastClockValue;
        public Entity Get(uint id)
        {
            if (!Entities.TryGetValue(id, out var e))
            {
                if (Entities.Count >= 16000) Entities.Clear();
                Entities[id] = e = new();
            }
            return e;
        }
    }
    private readonly Dictionary<string, Session> _sessions = new();
    private readonly Dictionary<int,string> _skillLabels=new();
    private string SkillLabel(int skill)
    {
        if(_skillLabels.TryGetValue(skill,out var label))return label;
        if(_skillLabels.Count>=4096)_skillLabels.Clear();
        return _skillLabels[skill]="Skill #"+skill;
    }
    private string? _active;
    private (Session Session,DateTimeOffset At)? _closedWorld;
    private long _internalMoves,_retainedParties,_profileRebindings;
    public long InternalMoves=>_internalMoves;
    public long RetainedParties=>_retainedParties;
    public long ProfileRebindings=>_profileRebindings;
    public int PartyMembers=>_active is {} partyKey&&_sessions.TryGetValue(partyKey,out var partySession)?partySession.Party?.Members.Count??0:0;
    private long _recognized, _rejected, _zoneRefreshes, _characterRecords;
    public long ZoneRefreshes=>_zoneRefreshes;
    public long CharacterRecords=>_characterRecords;
    public int? ZoneId=>_active is {} key&&_sessions.TryGetValue(key,out var session)?session.ZoneId:null;
    public bool IdentityRefreshPending=>_active is {} key&&_sessions.TryGetValue(key,out var session)&&session.AwaitingCharacter;
    public string IdentityState=>SelfName is not null?"Identified":IdentityRefreshPending?(PartyMembers>0?"Party retained; refreshing combat IDs":"Refreshing after teleport; waiting for own character record"):"Waiting for own character record";
    private readonly Dictionary<ushort,long> _opcodes = new();
    public IReadOnlyDictionary<string,long> OpcodeCounts => _opcodes.OrderByDescending(x => x.Value).Take(32).ToDictionary(x => x.Key.ToString("X4"), x => x.Value);
    public event Action<PlayerIdentity>? IdentityUpdated;
    public event Action<PlayerRating>? RatingUpdated;
    public long OwnScoreRecords {get;private set;}public long OwnGearScores {get;private set;}public long OwnCombatPowers {get;private set;}public long PartyScoreRecords {get;private set;}public long PartyGearScores {get;private set;}public long PartyCombatPowers {get;private set;}
    public string OwnScoreDetail {get;private set;}="Waiting for own-character score record";
    public long FullPlayerProfiles {get;private set;}public long ProfileScores {get;private set;}public long ProfilePowers {get;private set;}public long InspectScores {get;private set;}
    public event Action? OwnSceneLeft;
    public event Action<uint>? EntityRemoved;
    public event Action<uint,DateTimeOffset>? PlayerDefeated;
    public event Action<TargetHealth>? HealthUpdated;
    public event Action<uint,DateTimeOffset>? TargetDamaged;
    public long PartyLists {get;private set;}
    public long RejectedPartyLists {get;private set;}
    public event Action? ConnectionChanged;
    public event Action? CaptureContinuityLost;
    public void InvalidateContinuity(string streamId,DateTimeOffset at)
    {
        if(!_sessions.TryGetValue(streamId,out var s))return;s.PendingPets=null;s.PendingPetGains.Clear();if(_active!=streamId)return;
        s.Party=null;foreach(var e in s.Entities.Values){e.Hp=e.MaxHp=null;e.HpAt=null;}
        PartyUpdated?.Invoke(new([],at));CaptureContinuityLost?.Invoke();PetSyncLost?.Invoke("Capture gap; saved counts need a fresh collection.");
    }
    public event Action<PartyRoster>? PartyUpdated;
    public event Action<BossEngagement>? EngagementUpdated;
    public event Action<FieldBossList>? FieldBossesUpdated;
    public event Action<PetCollectionObservation>? PetCollectionUpdated;
    public event Action<PetGainObservation>? PetSoulsReceived;
    public event Action<string>? PetSyncLost;
    public long PetStateFrames {get;private set;}public long PetGainFrames {get;private set;}public int LastPetStateBytes {get;private set;}public string PetStateResult {get;private set;}="No collection frame observed";
    public long PetStates {get;private set;} public long PetGains {get;private set;} public long PetRejected {get;private set;} public long PetUnbound {get;private set;}
    public long FieldBossLists {get;private set;}
    public long UnboundFieldBossLists {get;private set;}
    public OdyleEnergy? Energy => _active is { } k && _sessions.TryGetValue(k,out var s) && s.Self.HasValue && !s.LoginPending ? s.Energy : null;
    public string? SelfClass => _active is {} k&&_sessions.TryGetValue(k,out var s)&&s.Self is {} id?s.Get(id).ClassName:null;
    public int? SelfServerId => _active is {} k&&_sessions.TryGetValue(k,out var s)&&s.Self is {} id?s.Get(id).ServerId:null;
    public IReadOnlyList<TicketBalance> Tickets => _active is {} k&&_sessions.TryGetValue(k,out var s)&&s.Self.HasValue&&!s.LoginPending?s.Tickets.Values.ToArray():[];
    public string Name => "Aion 2 Global • experimental 0.10.29 (MIT protocol port)";
    public bool HasGameStream => _active is not null;
    public string? SelfName => _active is { } k && _sessions.TryGetValue(k, out var s) && s.Self is { } id ? s.Get(id).Name : null;
    public long Recognized => _recognized;
    public long Rejected => _rejected;
    public bool TryDecode(AionFrame frame, DateTimeOffset receivedAt, out CombatEvent? combatEvent)
    {
        // Production must supply a stream context to isolate independent connections.
        combatEvent = null;
        return false;
    }
    public void Reset() { PetStateFrames=PetGainFrames=0;LastPetStateBytes=0;PetStateResult="No collection frame observed";PetStates=PetGains=PetRejected=PetUnbound=0;PetSyncLost?.Invoke("Capture restarted; waiting for a fresh collection."); _sessions.Clear();_skillLabels.Clear(); _active = null;_closedWorld=null;_internalMoves=_retainedParties=_profileRebindings=0; _recognized = _rejected = _zoneRefreshes = _characterRecords = 0; _opcodes.Clear();FullPlayerProfiles=ProfileScores=ProfilePowers=InspectScores=0;OwnScoreRecords=OwnGearScores=OwnCombatPowers=PartyScoreRecords=PartyGearScores=PartyCombatPowers=0;OwnScoreDetail="Waiting for own-character score record"; FieldBossLists=UnboundFieldBossLists=PartyLists=RejectedPartyLists=0;ConnectionChanged?.Invoke(); }
    public void ForgetStream(string streamId)
    {
        if(!_sessions.Remove(streamId,out var closed))return;
        if (_active == streamId)
        {
            // A world socket can close before the destination socket sends its zone frame.
            // Keep only a short-lived transfer anchor, never its combat IDs.
            Session? anchor=null;
            if(closed.VerifiedName is not null&&!closed.LoginPending)
            {
                anchor=new(){VerifiedName=closed.VerifiedName,VerifiedServer=closed.VerifiedServer,VerifiedClass=closed.VerifiedClass,VerifiedFaction=closed.VerifiedFaction,
                    PetIdentityConfirmed=closed.PetIdentityConfirmed,ZoneId=closed.ZoneId,Party=closed.Party,Energy=closed.Energy,CaptureSurface=closed.CaptureSurface,
                    ConnectionId=closed.ConnectionId,SourcePort=closed.SourcePort};
                foreach(var ticket in closed.Tickets)anchor.Tickets[ticket.Key]=ticket.Value;
            }
            _closedWorld=anchor is not null?(anchor,closed.LastSeen):null;
            _active=null;ConnectionChanged?.Invoke();
            if(_closedWorld is not null&&closed.Party is {} party)PartyUpdated?.Invoke(party);
        }
    }
    public void ExpirePendingWorld(DateTimeOffset at)
    {
        if(_closedWorld is not {} closed||at-closed.At<=TimeSpan.FromMinutes(2))return;
        _closedWorld=null;if(_active is null)ConnectionChanged?.Invoke();
    }
    public IReadOnlyList<CombatEvent> Decode(string streamId,AionFrame frame,DateTimeOffset at)=>Decode(streamId,frame,at,null,null);
    public IReadOnlyList<CombatEvent> Decode(string streamId,AionFrame frame,DateTimeOffset at,string? captureSurface,string? connectionId,ushort? sourcePort=null)
    {
        ExpirePendingWorld(at);
        if (!_sessions.TryGetValue(streamId,out var s))
        {
            if (_sessions.Count>=64)return [];
            _sessions[streamId]=s=new();
        }
        // A retired world connection must not reclaim the character on a late clock/login packet.
        if(s.Retired)return [];
        s.CaptureSurface=captureSurface??s.CaptureSurface;s.ConnectionId=connectionId??s.ConnectionId;s.SourcePort=sourcePort??s.SourcePort;
        s.LastSeen=at;
        var b=frame.Payload.AsSpan();
        if(frame.Opcode==PetProtocol.GainOpcode)PetGainFrames++;
        if(frame.Opcode==PetProtocol.StateOpcode)
        {
            PetStateFrames++;LastPetStateBytes=b.Length;
            if(PetProtocol.TryState(b,out var pets,out var petReason)){s.PendingPets=(pets,at);s.PendingPetGains.Clear();PetStateResult="Collection parsed; waiting for matching own identity";}
            else{PetRejected++;PetStateResult=petReason;if(_active==streamId)PetSyncLost?.Invoke("Unsupported collection: "+petReason);return [];}
        }
        if(frame.Opcode==PetProtocol.GainOpcode&&(!s.PetIdentityConfirmed||s.AwaitingCharacter||s.VerifiedName is null))
        {
            if(PetProtocol.TryGain(b,out uint pendingPet,out int pendingAmount)&&s.PendingPets.HasValue&&s.PendingPetGains.Count<64)
                s.PendingPetGains.Add((pendingPet,pendingAmount,at));
            else{s.PendingPets=null;s.PendingPetGains.Clear();if(_active==streamId){PetUnbound++;PetSyncLost?.Invoke("Soul gain before collection identity; re-enter the world to sync.");}}
            return [];
        }
        int scoreStart=-1;bool character=false,zone=false,partyRead=false;uint zoneCounter=0;int zoneId=0;
        uint? previousSelf=s.Self;
        string? previousName=s.VerifiedName;int? previousServer=s.VerifiedServer;
        if(frame.Opcode==0x2136)
        {
            // Upstream includes the two opcode bytes: counter at 2, zone at 6.
            // Our AionFrame payload excludes them, so these offsets are 0 and 4.
            if(b.Length<8||(zoneId=BinaryPrimitives.ReadInt32LittleEndian(b[4..]))<=0){_rejected++;return [];}
            zoneCounter=BinaryPrimitives.ReadUInt32LittleEndian(b);zone=true;
        }
        if(frame.Opcode==0x0b61)
        {
            if(OdyleEnergyReader.TryReadTickets(b,at,out var tickets))
            {
                s.Tickets.Clear();foreach(var ticket in tickets)s.Tickets[ticket.Id]=ticket;
                s.Energy=tickets.FirstOrDefault(t=>t.Id==OdyleEnergyReader.TicketId)is {} e?new(e.Regular,e.Bonus,e.ObservedAt):null;
                // Tickets can follow the own record within the same captured login bundle.
                // That ordering must not strand an otherwise verified pet collection.
                if(s.OwnRecordAt!=at){s.PetIdentityConfirmed=false;if(s.VerifiedName is not null&&!s.LoginPending){s.PendingPets=null;s.PendingPetGains.Clear();}}
                s.LoginPending=true;s.ProfileRebindAllowed=false;s.Party=null;
                if(_active==streamId&&!s.PetIdentityConfirmed)PetSyncLost?.Invoke("Entering world; waiting for a fresh collection.");
                if(_active==streamId)_closedWorld=null;
                if(_active==streamId)PartyUpdated?.Invoke(new([],at));
            }else _rejected++;
        }
        else if(frame.Opcode==0x0c61)
        {
            if(OdyleEnergyReader.TryReadTicketUpdate(b,at,out var ticket)&&ticket is {} t){s.Tickets[t.Id]=t;if(t.Id==OdyleEnergyReader.TicketId)s.Energy=new(t.Regular,t.Bonus,t.ObservedAt);}
            else _rejected++;
        }
        else if(frame.Opcode==0x0297){if(ReadParty(b,at)is {} roster){s.Party=roster;s.PartyScoresPending=true;PartyLists++;partyRead=true;}else RejectedPartyLists++;}
        if(frame.Opcode==0x0036&&b.Length==8)
        {
            long clock=BinaryPrimitives.ReadInt64LittleEndian(b);
            if(clock>at.AddDays(-1).ToUnixTimeMilliseconds()&&clock<at.AddDays(1).ToUnixTimeMilliseconds())
            {
                if(at-s.FirstClock>TimeSpan.FromSeconds(10)||clock<=s.LastClockValue)s.Clocks=0;
                if(s.Clocks==0)s.FirstClock=at;
                s.LastClockValue=clock;s.Clocks++;
            }
        }
        else if(frame.Opcode==0x3336)character=ReadCharacter(s,b,out scoreStart);

        if(_active!=streamId&&!character&&!zone&&s.Clocks<3)return [];
        bool changedConnection=false;
        Session? transfer=null;
        if(_active is {} active&&active!=streamId&&_sessions.TryGetValue(active,out var previous))
        {
            // Same capture interface + different TCP connection + explicit world/own record:
            // a zone handover can proceed while the old socket is still alive.
            // A second adapter observing the same socket must never duplicate combat.
            bool handover=(character||zone&&s.SourcePort.HasValue&&s.SourcePort==previous.SourcePort)&&s.CaptureSurface is not null&&
                s.CaptureSurface==previous.CaptureSurface&&s.ConnectionId is not null&&
                previous.ConnectionId is not null&&s.ConnectionId!=previous.ConnectionId;
            if(!handover&&at-previous.LastSeen<TimeSpan.FromSeconds(10))return [];
            transfer=previous;previous.Retired=true;
        }
        if(transfer is null&&_closedWorld is {} closed&&at>=closed.At&&at-closed.At<=TimeSpan.FromMinutes(2))transfer=closed.Session;
        if(transfer is not null&&(character||zone)&&!s.LoginPending&&!transfer.LoginPending&&
            s.CaptureSurface is not null&&s.CaptureSurface==transfer.CaptureSurface&&
            s.SourcePort.HasValue&&s.SourcePort==transfer.SourcePort&&s.ConnectionId!=transfer.ConnectionId)
        {
            bool sameOwner=character&&s.Self is {} current&&s.Get(current).Name==transfer.VerifiedName&&s.Get(current).ServerId==transfer.VerifiedServer;
            bool worldTransfer=zone&&(zoneId==transfer.ZoneId||WorldZoneFacts.IsInstance(zoneId)==true);
            if(sameOwner||worldTransfer)
            {
                s.PetIdentityConfirmed=transfer.PetIdentityConfirmed;s.VerifiedName=transfer.VerifiedName;s.VerifiedServer=transfer.VerifiedServer;s.VerifiedClass=transfer.VerifiedClass;s.VerifiedFaction=transfer.VerifiedFaction;
                s.ZoneId=transfer.ZoneId;s.Party??=transfer.Party;
                foreach(var ticket in transfer.Tickets) s.Tickets.TryAdd(ticket.Key,ticket.Value);
                s.Energy??=transfer.Energy;
                if(s.Party is{Members.Count:>0})_retainedParties++;
            }
            _closedWorld=null;
        }
        if(_active!=streamId)
        {
            changedConnection=_active is not null||transfer is not null||_closedWorld is not null;_active=streamId;
            if(character||zone)_closedWorld=null;
            if(changedConnection)ConnectionChanged?.Invoke();
            if(!character&&!zone&&s.Self is {} restored)NotifyIdentity(s,restored);
        }
        _opcodes[frame.Opcode]=_opcodes.GetValueOrDefault(frame.Opcode)+1;
        if(zone)
        {
            _recognized++;
            if(s.ZoneCounter==zoneCounter&&s.ZoneId==zoneId)return [];
            bool sameZone=s.ZoneId==zoneId;
            bool inside=WorldZoneFacts.IsInstance(zoneId)==true;
            bool keepParty=sameZone||inside;
            s.ZoneCounter=zoneCounter;s.ZoneId=zoneId;_zoneRefreshes++;
            // Doors, stage teleports and revivals inside the same instance need not
            // resend own-character or roster records. Their bindings stay valid on this TCP world.
            if(sameZone&&inside&&!changedConnection&&s.VerifiedName is not null&&!s.LoginPending)
            {
                _internalMoves++;OwnSceneLeft?.Invoke();return [];
            }
            s.AwaitingCharacter=true;s.ProfileRebindAllowed=s.VerifiedName is not null&&!s.LoginPending;
            s.Self=null;s.Entities.Clear();if(!keepParty)s.Party=null;
            if(!changedConnection)ConnectionChanged?.Invoke();
            if(s.Party is {} retained){PartyUpdated?.Invoke(retained);if(retained.Members.Count>0)_retainedParties++;}
            OwnSceneLeft?.Invoke();return [];
        }
        if(character)
        {
            var own=s.Get(s.Self!.Value);
            bool differentCharacter=previousName is not null&&(own.Name!=previousName||own.ServerId!=previousServer);
            bool rebound=previousSelf.HasValue&&previousSelf!=s.Self;
            if(differentCharacter||rebound)
            {
                s.Entities.Clear();s.Entities[s.Self.Value]=own;if(differentCharacter)s.Party=null;
                if(differentCharacter&&!s.LoginPending){s.Tickets.Clear();s.Energy=null;}
                if(!changedConnection)ConnectionChanged?.Invoke();
            }
            s.VerifiedName=own.Name;s.VerifiedServer=own.ServerId;s.VerifiedClass=own.ClassName;s.VerifiedFaction=own.Faction;
            s.OwnRecordAt=at;s.PetIdentityConfirmed=true;s.LoginPending=false;s.AwaitingCharacter=false;s.ProfileRebindAllowed=false;_characterRecords++;_recognized++;
            // Re-publish on every own-character record, including unchanged IDs after a teleport.
            NotifyIdentity(s,s.Self.Value);
            PublishPets(s);
            var ownRating=AutomaticRatingReader.Own(b,scoreStart,new(s.Self.Value,own.Name!,own.ClassName,true,own.ServerId,own.Faction),at,out var scoreDetail);OwnScoreDetail=scoreDetail;
            if(ownRating is not null){OwnScoreRecords++;if(ownRating.GearScore.HasValue)OwnGearScores++;if(ownRating.CombatPower.HasValue)OwnCombatPowers++;RatingUpdated?.Invoke(ownRating);}
            if(s.Party is {} initialParty){PartyUpdated?.Invoke(initialParty);if(s.PartyScoresPending){PublishPartyScores(initialParty);s.PartyScoresPending=false;}}
            return [];
        }
        switch(frame.Opcode)
        {
            case PetProtocol.StateOpcode:PublishPets(s);return [];
            case PetProtocol.GainOpcode:
                if(!PetProtocol.TryGain(b,out uint pet,out int amount)){PetRejected++;PetSyncLost?.Invoke("Unsupported soul-gain message; re-sync collection.");return [];}
                if(s.PetIdentityConfirmed&&!s.AwaitingCharacter&&s.VerifiedName is {} petOwner&&s.VerifiedServer is {} petServer)
                {PetGains++;_recognized++;PetSoulsReceived?.Invoke(new(petOwner,petServer,pet,amount,at));}
                else PetUnbound++;
                return [];
            case 0x0036:_recognized++;return [];
            case 0x0b61:case 0x0c61:_recognized++;return [];
            case 0x0297:if(partyRead&&s.Party is {} roster){_recognized++;PartyUpdated?.Invoke(roster);PublishPartyScores(roster);s.PartyScoresPending=false;foreach(var id in s.Entities.Where(e=>e.Value.Name is not null).Select(e=>e.Key).ToArray())NotifyIdentity(s,id);}return [];
            case 0x0191:
                if(FieldBossPacketReader.TryRead(b,at,out var list)&&list is not null){_recognized++;FieldBossLists++;if(SelfName is not null&&SelfServerId is {} server)FieldBossesUpdated?.Invoke(list with{ServerId=server});else UnboundFieldBossLists++;}
                else _rejected++;return [];
            case 0x218d:ReadEngagement(b,at);return [];
            case 0x3336:_rejected++;return [];
            case 0x4536:ReadNickname(s,b,at);return [];
            case 0x5036:ReadInspectRating(s,b,at);return [];
            case 0x048d:ReadAppearance(s,b,at);return [];
            case 0x4136:ReadSpawn(s,b,at);return [];
            case 0x4736:
                {int cursor=0;if(Id(b,ref cursor,out uint playerGone)&&b.Length-cursor==2&&b[cursor] is 1 or 2 or 10 or 16){if(s.Self==playerGone){OwnSceneLeft?.Invoke();}else{s.Entities.Remove(playerGone);EntityRemoved?.Invoke(playerGone);}_recognized++;}else _rejected++;return [];}
            case 0x4236:{int p=0;if(Id(b,ref p,out uint gone)){s.Entities.Remove(gone);if(s.Self==gone){s.Self=null;s.AwaitingCharacter=true;s.ProfileRebindAllowed=true;}EntityRemoved?.Invoke(gone);}return [];}
            case 0x008d:ReadHp(s,b,at);return [];
            case 0x0438:_recognized++;return ReadHit(s,b,at);
            case 0x0538:_recognized++;return ReadTick(s,b,at);
            default:return [];
        }
    }
    private void PublishPets(Session s)
    {
        if(s.PendingPets is not {} state||!s.PetIdentityConfirmed||s.AwaitingCharacter||s.VerifiedName is not {} name||s.VerifiedServer is not {} server)return;
        s.PendingPets=null;PetStateResult="Collection published to verified own character";PetStates++;_recognized++;PetCollectionUpdated?.Invoke(new(name,server,state.Counts,state.At));
        foreach(var gain in s.PendingPetGains){PetGains++;_recognized++;PetSoulsReceived?.Invoke(new(name,server,gain.Pet,gain.Amount,gain.At));}s.PendingPetGains.Clear();
    }
    private void ReadEngagement(ReadOnlySpan<byte> b, DateTimeOffset at)
    {
        int p = 0;
        if (!Id(b,ref p,out uint id)) { _rejected++; return; }
        if (b.Length == p + 2 && b[p] == 0 && b[p+1] == 0) { EngagementUpdated?.Invoke(new(id,false,false,at)); _recognized++; return; }
        if (b.Length == p + 2 && b[p] == 0 && b[p+1] == 1) { EngagementUpdated?.Invoke(new(id,true,false,at)); _recognized++; return; }
        if (b.Length == p + 10 && b[p] == 1 && b[p+1] == 1)
        {
            long ms = BinaryPrimitives.ReadInt64LittleEndian(b[(p+2)..]);
            if (ms > at.AddDays(-1).ToUnixTimeMilliseconds() && ms < at.AddDays(1).ToUnixTimeMilliseconds())
            { EngagementUpdated?.Invoke(new(id,true,true,DateTimeOffset.FromUnixTimeMilliseconds(ms))); _recognized++; return; }
        }
        _rejected++;
    }
    private static PartyRoster? ReadParty(ReadOnlySpan<byte> b, DateTimeOffset at)
    {
        var members = new HashSet<PartyMember>();
        for (int i = 2; i < b.Length; i++)
        {
            if (!NameAt(b,i,out var name)) continue;
            int end = i + 1 + b[i];
            if (end + 8 > b.Length) continue;
            int server = BinaryPrimitives.ReadUInt16LittleEndian(b[(i-2)..]);
            int code = BinaryPrimitives.ReadInt32LittleEndian(b[end..]);
            int level = BinaryPrimitives.ReadInt32LittleEndian(b[(end+4)..]);
            if (server is < 1000 or > 9999 || GlobalProtocolFacts.CharacterClass(code) is null || level is < 1 or > 200) continue;
            int? home=null;
            // MIT reference: optional home server after class, level and another u32;
            // accept the extra marker-byte variant only when followed by a repeated ID / 0x10xx.
            foreach(int offset in new[]{12,13})
            {
                if(end+offset+4>b.Length)continue;
                int h=BinaryPrimitives.ReadUInt16LittleEndian(b[(end+offset)..]);
                int next=BinaryPrimitives.ReadUInt16LittleEndian(b[(end+offset+2)..]);
                if(h is >=1000 and <=2999&&h%100 is >=1 and <=30&&(next==h||next>>8==0x10)){home=h;break;}
            }
            var rating=AutomaticRatingReader.Party(b,i,end,name,server,GlobalProtocolFacts.CharacterClass(code),at);
            if(rating is not null)home=server; // Account ID carries home server; a later pair may describe the visited world.
            members.Add(new(name,server,GlobalProtocolFacts.CharacterClass(code),level,home,rating?.GearScore,rating?.CombatPower,rating?.ServerId)); i = end + 7;
        }
        return members.Count > 0 && members.Count <= 24 ? new(members.ToArray(),at) : null;
    }

    private void PublishPartyScores(PartyRoster roster)
    {
        foreach(var member in roster.Members)if(member.ScoreServerId is {} server&&(member.GearScore.HasValue||member.CombatPower.HasValue))
        {
            PartyScoreRecords++;if(member.GearScore.HasValue)PartyGearScores++;if(member.CombatPower.HasValue)PartyCombatPowers++;
            RatingUpdated?.Invoke(new(0,member.Name,server,member.GearScore,member.CombatPower,roster.ObservedAt,"Automatic party roster (0x0297)"));
        }
    }
    private bool ReadCharacter(Session s, ReadOnlySpan<byte> b,out int scoreStart)
    {
        scoreStart=-1;int p = 0;
        if (!Id(b, ref p, out uint id)) return false;
        for (int k = p; k < Math.Min(b.Length - 11, p + 16); k++)
        {
            if (!NameAt(b, k, out var name)) continue;
            int after = k + 1 + b[k];
            if (after + 11 > b.Length || b[after + 6] is not (1 or 2)) continue;
            int code = BinaryPrimitives.ReadInt32LittleEndian(b[(after + 2)..]);
            string? cls = GlobalProtocolFacts.CharacterClass(code);
            int level = BinaryPrimitives.ReadInt32LittleEndian(b[(after + 7)..]);
            int server = BinaryPrimitives.ReadUInt16LittleEndian(b[after..]);
            if (cls is null || level is < 1 or > 200 || server is < 1000 or > 9999) continue;
            s.Self = id;scoreStart=after;
            var e = s.Get(id); e.Name = name; e.ClassName = cls; e.Npc = null; e.ServerId = server;e.Faction=b[after+6];e.PlayerVerified=true;
            return true;
        }
        return false;
    }
    // Inspect scores are joined only to one already verified name/server/class. They cannot create an identity.
    private void ReadInspectRating(Session s,ReadOnlySpan<byte> b,DateTimeOffset at)
    {
        if(b.Length<38||!NameAt(b,3,out string name))return;int after=4+b[3];
        if(after+18>b.Length||GlobalProtocolFacts.CharacterClass(BinaryPrimitives.ReadInt32LittleEndian(b[after..]))is not {} cls)return;
        int level=BinaryPrimitives.ReadInt32LittleEndian(b[(after+6)..]),score=BinaryPrimitives.ReadInt32LittleEndian(b[(after+14)..]);if(level is <1 or >200||score is <=0 or >=100000)return;
        var servers=new HashSet<int>();for(int p=after+18;p+4<Math.Min(b.Length,after+80);p++){int server=BinaryPrimitives.ReadUInt16LittleEndian(b[p..]);if(server is >=1000 and <=3000&&server%100 is >=1 and <=30&&NameAt(b,p+2,out string guild)&&guild!=name)servers.Add(server);}
        if(servers.Count!=1)return;int exactServer=servers.Single();var matches=s.Entities.Where(e=>e.Value.PlayerVerified&&e.Value.Npc is null&&e.Value.Name==name&&e.Value.ServerId==exactServer&&e.Value.ClassName==cls).ToArray();
        uint? actor=matches.Length==1?matches[0].Key:null;
        // A current party roster verifies name, home server and class even while world IDs refresh.
        // Keep the score in the exact profile cache; do not create a player identity or a combat ID.
        if(matches.Length==0&&s.Party?.Members.Count(m=>m.Name==name&&(m.HomeServerId??m.ServerId)==exactServer&&m.ClassName==cls)==1)actor=0;
        if(actor is {} id){InspectScores++;RatingUpdated?.Invoke(new(id,name,exactServer,score,null,at,"Observed inspect score (experimental)"));_recognized++;}
    }
    // MIT full-profile guild block: server | nonzero guild ID | 00 00 | repeated server | guild name.
    private static int? ProfileServer(ReadOnlySpan<byte> b,int afterName,int? faction)
    {
        var servers=new HashSet<int>();for(int p=afterName+5;p+12<b.Length;p++)
        {
            int server=BinaryPrimitives.ReadUInt16LittleEndian(b[p..]);if(PlayerServerFacts.Faction(server)!=faction||faction is not (1 or 2)||b[p+6]!=0||b[p+7]!=0||BinaryPrimitives.ReadUInt16LittleEndian(b[(p+8)..])!=server||BinaryPrimitives.ReadUInt32LittleEndian(b[(p+2)..])==0||!NameAt(b,p+10,out _))continue;servers.Add(server);
        }return servers.Count==1?servers.Single():null;
    }
    private void ReadNickname(Session s, ReadOnlySpan<byte> b,DateTimeOffset at)
    {
        int p = 0;
        if (!Id(b, ref p, out uint id)) { _rejected++; return; }
        for (int k = p; k < Math.Min(b.Length - 4, p + 24); k++)
            if (NameAt(b, k, out string name))
            {
                if(s.Get(id).Npc is not null){s.Get(id).NpcLabel=name;_recognized++;return;}
                SetPlayerName(s,id,name,null);
                int factionAt=k+1+b[k]+4;if(factionAt<b.Length&&b[factionAt] is 1 or 2)s.Get(id).Faction=b[factionAt];
                int afterName=k+1+b[k];if(afterName+4<=b.Length&&GlobalProtocolFacts.CharacterClass(BinaryPrimitives.ReadInt32LittleEndian(b[afterName..])) is {} cls)s.Get(id).ClassName=cls;
                if(PlayerRatingReader.Read(id,name,s.Get(id).ServerId,b,afterName,at)is {} rating)
                {
                    if(s.Get(id).ServerId is null&&ProfileServer(b,afterName,s.Get(id).Faction)is {} server){s.Get(id).ServerId=server;rating=rating with{ServerId=server};}
                    FullPlayerProfiles++;if(rating.GearScore.HasValue)ProfileScores++;if(rating.CombatPower.HasValue)ProfilePowers++;
                    // A full, class/faction-valid profile with a unique level anchor is stronger than a bare nickname.
                    // Known NPCs returned above; this proof never grants self or combat-party membership.
                    s.Get(id).PlayerVerified=true;RatingUpdated?.Invoke(rating);
                }
                _recognized++; NotifyIdentity(s, id); return;
            }
        _rejected++;
    }
    private void ReadAppearance(Session s, ReadOnlySpan<byte> b,DateTimeOffset at)
    {
        int p = 0;
        if (!Var(b, ref p, out uint victim) || p + 4 > b.Length) { _rejected++; return; }
        uint killingSkill=BinaryPrimitives.ReadUInt32LittleEndian(b[p..]);p += 4;
        if (!Id(b, ref p, out uint id) || p + 3 > b.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(b[p..]) is < 1000 or > 9999 ||
            !NameAt(b, p + 2, out var name)) { _rejected++; return; }
        // MIT reference 8c14501: the validated appearance prefix can also name a defeated victim.
        if(victim is >0 and <=10_000_000&&killingSkill is >0 and <=300_000_000&&s.Entities.TryGetValue(victim,out var fallen)&&fallen.PlayerVerified)PlayerDefeated?.Invoke(victim,at);
        int server=BinaryPrimitives.ReadUInt16LittleEndian(b[p..]);if(s.Get(id).Npc is not null)EntityRemoved?.Invoke(id);SetPlayerName(s,id,name,server);s.Get(id).PlayerVerified=true;
        // Only a previously verified own profile plus a fresh exact name/server can
        // restore self after a world transfer. A bare nickname never grants self.
        if(s.AwaitingCharacter&&s.ProfileRebindAllowed&&!s.LoginPending&&name==s.VerifiedName&&server==s.VerifiedServer&&
            s.Entities.Count(e=>e.Value.Name==name&&e.Value.ServerId==server)==1)
        {
            s.Self=id;s.Get(id).ClassName=s.VerifiedClass;s.Get(id).Faction=s.VerifiedFaction;s.AwaitingCharacter=false;s.ProfileRebindAllowed=false;_profileRebindings++;
        }
        _recognized++;NotifyIdentity(s,id);
    }
    private void SetPlayerName(Session s,uint id,string name,int? server)
    {
        if(s.Self==id&&(name!=s.VerifiedName||server.HasValue&&server!=s.VerifiedServer))
        {s.Self=null;s.AwaitingCharacter=true;s.ProfileRebindAllowed=true;EntityRemoved?.Invoke(id);}
        var e=s.Get(id);if(e.Name is not null&&(e.Name!=name||server.HasValue&&e.ServerId.HasValue&&server!=e.ServerId)){e.ServerId=null;e.Faction=null;e.ClassName=null;e.PlayerVerified=false;EntityRemoved?.Invoke(id);}
        e.Name=name;e.Npc=null;e.Owner=null;if(server.HasValue)e.ServerId=server;
    }
    private void NotifyIdentity(Session s, uint id)
    {
        var e = s.Get(id);
        if(e.Name is not {} name||e.Npc is not null)return;
        var matches=s.Party?.Members.Where(m=>m.Name==name&&(e.ServerId is null||m.ServerId==e.ServerId||m.HomeServerId==e.ServerId)).ToArray();
        var cls=e.ClassName??(matches is {Length:1}?matches[0].ClassName:null);
        IdentityUpdated?.Invoke(new(id,name,cls,s.Self==id,e.ServerId,e.Faction,e.PlayerVerified));
    }
    private void ReadSpawn(Session s, ReadOnlySpan<byte> b, DateTimeOffset at)
    {
        int p = 0;
        if (!Id(b, ref p, out uint id) || p + 7 > b.Length) { _rejected++; return; }
        if (b[p + 2] == 1)
        {
            if (!NameAt(b, p + 3, out _)) { _rejected++; return; }
            p += 1 + b[p + 3];
            if (p + 7 > b.Length) { _rejected++; return; }
        }
        p += 3;
        int npc = BinaryPrimitives.ReadInt32LittleEndian(b[p..]);
        if (npc <= 0) { _rejected++; return; }
        if(s.Self==id){s.Self=null;s.AwaitingCharacter=true;s.ProfileRebindAllowed=true;}
        EntityRemoved?.Invoke(id);
        var e = s.Get(id); e.Npc = npc;e.NpcLabel=null; e.Name = null; e.ClassName = null;e.Faction=null;e.ServerId=null;e.PlayerVerified=false; e.Owner = null; e.Hp = e.MaxHp = null; e.HpAt = null;
        int after = p + 4;
        ReadOnlySpan<byte> ownerMark = [255,255,255,255,255,255,255,255];
        int marker = b[after..].IndexOf(ownerMark);
        int ownerAt = marker < 0 ? -1 : after + marker + 16;
        if (ownerAt >= 0 && Id(b, ref ownerAt, out uint owner) && owner != id) e.Owner = owner;
        foreach (int skip in new[] {21, 33})
        {
            int q = after + skip;
            if (Var(b, ref q, out uint hp) && Var(b, ref q, out uint max) && max > 0 && hp <= max &&
                q + 8 <= b.Length && b.Slice(q, 8).SequenceEqual(new byte[] {100,0,0,0,100,0,0,0}))
            { e.Hp = hp; e.MaxHp = max; e.HpAt = at; break; }
        }
        _recognized++;
    }
    private void ReadHp(Session s, ReadOnlySpan<byte> b, DateTimeOffset at)
    {
        int p = 0;
        if (!Id(b, ref p, out uint id) || p >= b.Length) { _rejected++; return; }
        byte format = b[p++];
        if ((format & ~3) != 0) { _rejected++; return; }
        if ((format & 1) != 0)
        {
            if (p >= b.Length) { _rejected++; return; }
            p += 1 + b[p] * 5;
        }
        long? hp = null, max = null;
        if ((format & 2) != 0)
        {
            if (p >= b.Length) { _rejected++; return; }
            int n = b[p++];
            if (p + n * 9 > b.Length) { _rejected++; return; }
            for (int i = 0; i < n; i++, p += 9)
            {
                long value = BinaryPrimitives.ReadInt64LittleEndian(b[(p + 1)..]);
                if (b[p] == 0) hp = value;
                if (b[p] == 7) max = value;
            }
        }
        if (p != b.Length || hp < 0 || max <= 0 || hp > max) { _rejected++; return; }
        var e = s.Get(id); e.Hp = hp ?? e.Hp; e.MaxHp = max ?? e.MaxHp;
        if (e.Hp > e.MaxHp) e.MaxHp = null; // never manufacture a maximum from observed health
        if (hp.HasValue) e.HpAt = at;
        _recognized++; HealthUpdated?.Invoke(new(id, e.Hp, e.MaxHp, e.HpAt));
    }
    private IReadOnlyList<CombatEvent> ReadHit(Session s, ReadOnlySpan<byte> b, DateTimeOffset at)
    {
        int p = 0;
        if (!Id(b, ref p, out uint target) || p + 2 > b.Length) return Reject();
        int flags = b[p]; p += 2;
        if ((flags & 4) == 0) return []; // companion notices do not carry actual damage
        if (!Id(b, ref p, out uint actor) || p + 6 > b.Length) return Reject();
        int skill = BinaryPrimitives.ReadInt32LittleEndian(b[p..]); p += 4;
        if (GlobalProtocolFacts.IsNonDamage(skill)) return [];
        bool critical = b[p + 1] == 3;
        int marker = -1;
        for (int i = p + 2; i + 4 <= b.Length; i++)
            if (b[i] is >= 1 and <= 9 && b[i+1] == 0 && b[i+2] == 0 && b[i+3] == 0) { marker = i; break; }
        int q = marker + 4;
        if (marker < 0 || !Var(b, ref q, out _) || !Var(b, ref q, out uint amount) || amount is 0 or > 100000000) return Reject();
        bool heal = GlobalProtocolFacts.IsHeal(skill) && s.Get(target).Npc is null;
        if (!heal)TargetDamaged?.Invoke(target,at);
        if (!heal && actor == target) return [];
        return Emit(s, actor, target, skill, amount, heal, critical && !heal ? HitTags.Critical : HitTags.None, at, b[marker]);
    }
    private IReadOnlyList<CombatEvent> ReadTick(Session s, ReadOnlySpan<byte> b, DateTimeOffset at)
    {
        int p = 0;
        if (!Id(b, ref p, out uint target) || p >= b.Length) return Reject();
        int flags = b[p++];
        if ((flags & 8) == 0 || (flags & ~15) != 0) return [];
        // 0x09 is the initial HoT announcement, not an actual tick.
        if ((flags & 2) == 0) return [];
        if (!Id(b, ref p, out uint actor) || !Var(b, ref p, out _) || p + 4 > b.Length) return Reject();
        uint effect = BinaryPrimitives.ReadUInt32LittleEndian(b[p..]); p += 4;
        uint amount = 0, healed = 0;
        if (((flags & 2) != 0 && !Var(b, ref p, out amount)) || ((flags & 1) != 0 && !Var(b, ref p, out healed)) || p + 4 != b.Length) return Reject();
        int skill = BinaryPrimitives.ReadInt32LittleEndian(b[p..]);
        if (GlobalProtocolFacts.IsNonDamage(skill)) return [];
        bool heal = GlobalProtocolFacts.IsHeal(skill) || KnownPlayer(s, actor) && KnownPlayer(s, target);
        if (heal)
            return healed is > 0 and <= 100000000 ? Emit(s, actor, target, skill, healed, true, HitTags.Dot, at) : [];
        // NPC-triggered reflect/heal effects and shield shares are not player DOT damage.
        if(amount>0)TargetDamaged?.Invoke(target,at);
        if (effect < 1000000000 || actor == target || amount is 0 or > 100000000) return [];
        return Emit(s, actor, target, skill, amount, false, HitTags.Dot, at);
    }
    private IReadOnlyList<CombatEvent> Reject() { _rejected++; return []; }
    private static bool KnownPlayer(Session s, uint id) => s.Self == id || s.Get(id).Name is not null;
    private IReadOnlyList<CombatEvent> Emit(Session s, uint actor, uint target, int skill, uint amount, bool heal, HitTags tags, DateTimeOffset at, int hits = 1)
    {
        var original = s.Get(actor);
        uint source = original.Owner is { } owner && KnownPlayer(s, owner) ? owner : actor;
        var player = s.Get(source);
        string? cls = player.ClassName ?? GlobalProtocolFacts.SkillClass(skill);
        if (source == actor && original.Npc is not null || !KnownPlayer(s, source) && cls is null) return [];
        if (heal && s.Get(target).Owner is not null) return [];
        if(player.ClassName is null&&cls is not null){player.ClassName=cls;NotifyIdentity(s,source);}
        var dst = s.Get(target);
        string name = player.Name ?? (player.AnonymousName??="#" + source);
        return [new CombatEvent(at, source, name, target, targets?.NameOf(dst.Npc) ?? dst.Name ?? (dst.Npc is { } npc ? dst.NpcLabel??="NPC #" + npc : dst.AnonymousName??="#" + target),
            skill, amount, heal ? CombatKind.Heal : CombatKind.Damage, tags,
            SkillName: SkillLabel(skill), ActorClass: cls, IsSelf: s.Self == source,
            TargetIsBoss: targets?.IsTrainingDummy(dst.Npc)!=true&&(targets?.IsBoss(dst.Npc)==true||timing?.Find(dst.Npc).HasValue == true), TargetNpcCode: dst.Npc, TargetCurrentHp: dst.Hp, TargetMaxHp: dst.MaxHp,
            HitCount: hits, TargetHpObservedAt: dst.HpAt, TargetIsTrainingDummy:targets?.IsTrainingDummy(dst.Npc)==true)];
    }
    private static bool Id(ReadOnlySpan<byte> b, ref int p, out uint id) => Var(b, ref p, out id) && id is > 0 and <= 10000000;
    private static bool Var(ReadOnlySpan<byte> b, ref int p, out uint value)
    {
        value = 0;
        for (int i = 0; i < 5 && p >= 0 && p < b.Length; i++)
        {
            byte x = b[p++];
            if (i == 4 && (x & 0xf0) != 0) return false;
            value |= (uint)(x & 127) << (7 * i);
            if ((x & 128) == 0) return true;
        }
        return false;
    }
    private static bool NameAt(ReadOnlySpan<byte> b, int p, out string name)
    {
        name = "";
        if (p < 0 || p >= b.Length || b[p] is < 2 or > 24 || p + 1 + b[p] > b.Length) return false;
        var bytes = b.Slice(p + 1, b[p]);
        if (!System.Text.Unicode.Utf8.IsValid(bytes)) return false;
        var candidate = Encoding.UTF8.GetString(bytes);
        if (candidate.Length < 2 || !candidate.All(char.IsLetterOrDigit)) return false;
        name = candidate; return true;
    }
}
