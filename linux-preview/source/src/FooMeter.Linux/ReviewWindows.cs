using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using AscendAPP.Core.Combat;

namespace FooMeter.Linux;

internal static class ReviewWindows
{
    public static Window Breakdown(EncounterSnapshot fight,PlayerSnapshot player)
    {
        var title=Ui.Text($"{player.Name} · {player.ClassName ?? "Unknown class"}",20);
        var totals=Ui.Text($"{player.Damage:N0} damage · {player.Dps:N0} DPS · {player.Healing:N0} healing\nCrit {player.CritRate:P1} · {player.Hits} hits · {player.Burst5s:N0} burst DPS");
        var sort=new ComboBox {ItemsSource=new[]{"Damage","Healing","Hits","Critical hits","Largest hit"},SelectedIndex=0};
        var skills=new StackPanel{Spacing=6};
        void Refresh() {
            skills.Children.Clear();
            Func<SkillSnapshot,long> key=sort.SelectedIndex switch{1=>s=>s.Healing,2=>s=>s.Hits,3=>s=>s.CriticalHits,4=>s=>s.MaxHit,_=>s=>s.Damage};
            foreach(var s in player.Skills.OrderByDescending(key)) skills.Children.Add(Ui.Stack(
                Ui.Text(s.Name??$"Skill #{s.SkillId}",15),Ui.Text($"Damage {s.Damage:N0} · Healing {s.Healing:N0} · Hits {s.Hits} · Crits {s.CriticalHits}\nAverage {s.AverageHit:N0} · Max {s.MaxHit:N0} · Unverified healing {s.UnverifiedHealing:N0}",12)));
        }
        sort.SelectionChanged+=(_,_)=>Refresh();Refresh();
        var graph=new Canvas{Height=120,Width=480,Background=Brush.Parse("#10131A")};
        var points=player.Timeline??[];
        if(points.Count>1) {
            double max=Math.Max(1,points.Max(p=>p.Damage)),duration=Math.Max(1,points.Max(p=>p.Second));
            graph.Children.Add(new Polyline {Stroke=Brush.Parse("#75D6ED"),StrokeThickness=2,Points=new Avalonia.Collections.AvaloniaList<Point>(points.Select(p=>new Point(p.Second/duration*480,115-p.Damage/max*105)))});
        }
        return Ui.Window("FooMeter · Skill breakdown",Ui.Scroll(Ui.Stack(title,totals,Ui.Text("Damage timeline (per second)"),graph,sort,skills)));
    }
    private sealed record Row(EncounterSnapshot Fight,bool Clock24) { public override string ToString()=>$"{Fight.StartedAt.ToLocalTime().ToString(Clock24?"yyyy-MM-dd HH:mm":"yyyy-MM-dd hh:mm tt")} · {EncounterReview.Target(Fight)} · {Fight.PartyDps:N0} DPS"; }
    public static Window History(LinuxMeter meter)
    {
        var search=new TextBox{Watermark="Search targets, players or classes"};
        var list=new ListBox {SelectionMode=SelectionMode.Multiple,MinHeight=200,MaxHeight=260};
        var details=Ui.Text("Select an encounter; select two to compare.");
        var window=Ui.Window("FooMeter · Fight history",new Border(),760,680);
        var status=Ui.Text("");
        void Refresh() {list.ItemsSource=EncounterReview.Search(meter.View().History,search.Text,meter.Preferences.HideNames).Select(f=>new Row(f,meter.Preferences.Clock24)).ToArray();}
        EncounterSnapshot Selected()=> (list.SelectedItem as Row)?.Fight??throw new InvalidOperationException("Select a completed encounter first.");
        list.SelectionChanged+=(_,_)=> {
            if(list.SelectedItem is Row row)details.Text=$"{EncounterReview.Target(row.Fight)} · {row.Fight.DurationSeconds:N1}s\n"+string.Join("\n",row.Fight.Players.Select(p=>$"{p.Name} · {p.Dps:N0} DPS · {p.Damage:N0} damage · {p.Healing:N0} healing"));
            var selected=list.SelectedItems?.Cast<Row>().ToArray()??[];
            if(selected.Length==2) {var comparison=EncounterReview.Compare(selected[0].Fight,selected[1].Fight);details.Text+=$"\n\n{comparison.Reason}\nDPS change: {comparison.PartyDpsChange:P1} · Duration: {comparison.DurationChange:P1}";}
        };
        search.TextChanged+=(_,_)=>Refresh();
        var buttons=new WrapPanel();
        buttons.Children.Add(Ui.Button("Refresh",Refresh));
        buttons.Children.Add(Ui.Async("Export CSV",()=>Ui.Export(window,Selected(),false),e=>status.Text=e.Message));
        buttons.Children.Add(Ui.Async("Export JSON",()=>Ui.Export(window,Selected(),true),e=>status.Text=e.Message));
        buttons.Children.Add(Ui.Async("Copy",async()=>{await window.Clipboard!.SetTextAsync(Ui.Share(Selected(),meter.Preferences.CopyFormat=="Multi-line",meter.Preferences.HideNames));status.Text="Copied";},e=>status.Text=e.Message));
        buttons.Children.Add(Ui.Button("Player details",()=>{try {var fight=Selected();var players=new ListBox{ItemsSource=fight.Players.Select(p=>p.Name).ToArray()};var pick=Ui.Window("Choose player",players,360,360);players.DoubleTapped+=(_,_)=>{if(players.SelectedIndex>=0)Breakdown(fight,fight.Players[players.SelectedIndex]).Show(window);};pick.Show(window);}catch(Exception e){status.Text=e.Message;}}));
        buttons.Children.Add(Ui.Button("Delete selected…",()=> {
            var selected=list.SelectedItems?.Cast<Row>().ToArray()??[]; if(selected.Length==0)return;
            var confirm=Ui.Window("Delete encounters?",new Border(),380,180);
            confirm.Content=Ui.Stack(Ui.Text($"Permanently delete {selected.Length} saved encounters?"),Ui.Button("Delete",()=>{foreach(var r in selected)meter.DeleteFight(r.Fight.Id);Refresh();confirm.Close();}),Ui.Button("Cancel",()=>confirm.Close()));confirm.ShowDialog(window);
        }));
        window.Content=new Border{Padding=new(16),Child=Ui.Stack(search,buttons,Ui.Scroll(list),Ui.Scroll(details),status)};Refresh();return window;
    }
}
