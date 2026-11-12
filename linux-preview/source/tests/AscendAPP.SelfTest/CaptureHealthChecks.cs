using AscendAPP.Core.Protocol;
static class CaptureHealthChecks
{
    public static void Run(Action<bool,string> check)
    {
        var t=DateTimeOffset.UtcNow;var none=new CaptureDiagnostics(0,0,0,0,0,0,false,0,0,0,0);
        CaptureHealth H(CaptureDiagnostics d,bool running=true,bool pending=false,bool admitted=false,bool active=false,string mode="LIVE")=>CaptureHealth.Evaluate(running,mode,d,pending,admitted,active,t);
        check(H(none,false).State=="Stopped"&&CaptureHealth.Evaluate(false,"LIVE",none,false,false,false,t,"Npcap missing").State=="Error","Stopped capture and an actual start failure have different status states");
        check(H(none).State=="NoPackets"&&H(none with{Packets=10}).State=="NoPayloads","The badge distinguishes no packets from packets without TCP payloads");
        var payload=none with{Packets=10,Segments=5,PayloadBytes=100,LastPayloadAt=t};
        check(H(payload).State=="NoFrames"&&H(payload with{Frames=3}).State=="NoEvents","The badge distinguishes unrecognized framing from frames with no decoded combat");
        var events=payload with{Frames=3,Decoded=2};check(H(events).State=="UnknownPlayer"&&H(events,pending:true).State=="CharacterRefresh","Unknown own identity and teleport refresh get explicit explanations");
        check(H(events with{SelfKnown=true}).State=="Filtered"&&H(events with{SelfKnown=true},admitted:true).State=="Ready","Decoded but excluded combat does not masquerade as admitted meter data");
        check(H(events with{SelfKnown=true,UnknownActors=100},admitted:true,active:true).State=="Combat","Historical unknown actors do not leave a recovered current identity permanently in warning state");
        check(H(events with{SelfKnown=true,LastPayloadAt=t.AddSeconds(-16)},admitted:true).State=="Quiet","Old totals with no recent TCP payload are labelled quiet rather than live");
        check(H(events,mode:"DEMO").State=="Demo"&&H(events,mode:"REPLAY").State=="Replay","Demo and imported data are clearly separate from live capture status");
        check(H(payload).Detail.Contains("TCP payloads")&&H(payload).Detail.Contains("diagnostics"),"Capture tooltips explain the counters and how to investigate");
    }
}
