using System.Windows;
using System.Windows.Controls;
using AscendAPP.Core.Combat;

namespace AscendAPP.Desktop;

public partial class SkillBreakdownWindow : Window
{
    private readonly EncounterSnapshot _fight;
    private readonly bool _healing,_hideNames;
    private sealed record Pick(PlayerSnapshot Player,string Label);
    public sealed record SkillLine(string Name, int Hits, string Damage, string Crit, string Max);
    public SkillBreakdownWindow(EncounterSnapshot fight,string? selectedName=null,bool healing=false,bool hideNames=false,uint? selectedActor=null)
    {
        _fight = fight;_healing=healing;_hideNames=hideNames;
        InitializeComponent();
        EncounterText.Text = fight.Boss?.Name ?? fight.TargetName ?? "Encounter";
        AmountHeading.Text=healing?"HEALING":"DAMAGE";
        PlayerSelector.DisplayMemberPath="Label";PlayerSelector.ItemsSource = fight.Players.Select(p=>new Pick(p,Shown(p))).ToArray();
        PlayerSelector.SelectedIndex = selectedActor is {} id?Math.Max(0,fight.Players.ToList().FindIndex(p=>p.ActorId==id)):selectedName is null?0:Math.Max(0,fight.Players.ToList().FindIndex(p=>p.Name==selectedName));
    }
    private string Shown(PlayerSnapshot p)=>_hideNames&&!p.IsSelf?p.ClassName??"Unknown":p.Name;
    private void PlayerSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if(PlayerSelector.SelectedItem is not Pick item)return;var p=item.Player;
        Summary.Text = _healing?$"{Shown(p)} · {p.Hps(_fight.DurationSeconds):N0} eHPS · {p.Healing:N0} scored · {p.RawHealing:N0} raw · {p.ExcludedHealing:N0} excluded · {p.UnverifiedHealing:N0} unverified":$"{Shown(p)}   •   {p.Dps:N0} DPS   •   {p.Damage:N0} total   •   {p.CritRate:P1} crit   •   {p.Burst5s:N0} burst / 5s";
        DataContext = p.Skills.Where(s=>_healing?s.Healing>0:s.Damage>0).OrderByDescending(s => _healing?s.Healing:s.Damage).Select(s =>
            new SkillLine(s.Name ?? $"Skill #{s.SkillId}", _healing?s.HealingHits:s.Hits, (_healing?s.Healing:s.Damage).ToString("N0"),
                _healing?(s.HealingHits==0?"—":((double)s.HealingCriticalHits/s.HealingHits).ToString("P1")):(s.Hits == 0 ? "—" : ((double)s.CriticalHits / s.Hits).ToString("P1")),(_healing?s.MaxHeal:s.MaxHit).ToString("N0"))).ToArray();
    }
}
