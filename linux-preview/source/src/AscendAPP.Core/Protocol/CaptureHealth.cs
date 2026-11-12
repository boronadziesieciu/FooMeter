namespace AscendAPP.Core.Protocol;
public sealed record CaptureHealth(string State,string Label,string Color,string Detail)
{
    public static CaptureHealth Evaluate(bool running,string mode,CaptureDiagnostics d,bool identityPending,bool admitted,bool active,DateTimeOffset now,string? failure=null)
    {
        string state,label,color="#D2B67D",reason;
        if(mode.Contains("DEMO",StringComparison.OrdinalIgnoreCase))return new("Demo","Demo","#A9A0D2","Synthetic demo data; native capture is not running.");
        if(mode=="REPLAY")return new("Replay","Replay","#A9A0D2","Imported session replay; native capture is not running.");
        if(!running){bool error=!string.IsNullOrWhiteSpace(failure);return new(error?"Error":"Stopped",error?"Capture error":"Stopped",error?"#DA8C96":"#87989A",error?failure!:"Capture is stopped. Open Settings → General to start it.");}
        if(d.Packets==0){state="NoPackets";label="No packets";reason="Capture is open but no packets have arrived. Check the adapter, Npcap and game connection.";}
        else if(d.Segments==0){state="NoPayloads";label="No payloads";reason="Packets arrived without TCP payloads. Check the selected adapter or port filter.";}
        else if(d.LastPayloadAt is {} at&&now-at>TimeSpan.FromSeconds(15)){state="Quiet";label="Traffic quiet";reason="No TCP payload has arrived for over 15 seconds. Previous totals are retained; this does not confirm a live game stream.";}
        else if(d.Frames==0){state="NoFrames";label="No frames";reason="TCP payloads arrived without recognized frames. The traffic may be unrelated, incomplete or unsupported.";}
        else if(identityPending){state="CharacterRefresh";label="Refreshing";reason="Refreshing your character after a world change. Wait for the game's own character record.";}
        else if(d.Decoded==0){state="NoEvents";label="No events";reason="Frames arrived without decoded combat events. This can be normal outside combat; attack a boss or training dummy. Unsupported messages also remain unscored.";}
        else if(!d.SelfKnown){state="UnknownPlayer";label="Character?";reason="Combat events decoded but your own character is not identified. Enter or change zone with capture running.";}
        else if(!admitted){state="Filtered";label="Filtered";color="#8CBACD";reason="Combat events decoded; none admitted to this encounter. Only self/confirmed party against bosses and dummies count.";}
        else{state=active?"Combat":"Ready";label=active?"In combat":"Ready";color="#92D4C7";reason=active?"Admitted combat events and current character identity are available.":"Decoded data and character identity are available. Waiting for eligible combat.";}
        return new(state,label,color,reason+$"\nPackets {d.Packets:N0} · TCP payloads {d.Segments:N0} · frames {d.Frames:N0} · combat events {d.Decoded:N0}\nHistorical unknown actors {d.UnknownActors:N0} · errors {d.Errors:N0} · gaps {d.Gaps:N0}\nClick to open diagnostics. These counters do not verify complete live decoding.");
    }
}
