using System.Windows;
using System.Windows.Controls;
using AscendAPP.Core.Combat;
namespace AscendAPP.Desktop;
public partial class FightHistoryWindow : Window
{
    private readonly bool _hideNames,_clock24;private readonly List<EncounterSnapshot> _all;private readonly Func<Guid,bool>? _delete;private readonly Func<bool>? _clear;private readonly Func<string,bool> _confirm;private bool _changing;
    private sealed record Item(string Description,EncounterSnapshot Fight);
    public FightHistoryWindow(IReadOnlyList<EncounterSnapshot> fights,bool clock24=true,bool hideNames=false,Func<Guid,bool>? onDelete=null,Func<bool>? onClear=null,string? storageStatus=null,Func<string,bool>? confirm=null)
    {
        _all=fights.Where(f=>!f.IsActive).OrderByDescending(f=>f.StartedAt).ToList();_delete=onDelete;_clear=onClear;_confirm=confirm??(message=>MessageBox.Show(this,message,"Saved fight history",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes);_hideNames=hideNames;_clock24=clock24;
        InitializeComponent();HistoryStorage.Text=storageStatus??"Local encounter history";Filter();
    }
    private Item Row(EncounterSnapshot f)=>new($"{f.StartedAt.ToLocalTime().ToString(_clock24?"g":"M/d/yyyy h:mm tt")}  ·  {EncounterReview.Target(f)}  ·  {f.DurationSeconds:0}s{(f.Interrupted?" · partial":"")}  ·  {EncounterReview.Rate(f):N0} party eDPS",f);
    private void Search_Changed(object sender,TextChangedEventArgs e){if(Fights is not null)Filter();}
    private void Filter()
    {
        var id=(Fights.SelectedItem as Item)?.Fight.Id;var items=EncounterReview.Search(_all,SearchBox.Text,_hideNames).Select(Row).ToArray();
        Fights.ItemsSource=items;Fights.SelectedItem=items.FirstOrDefault(i=>i.Fight.Id==id)??items.FirstOrDefault();
        ResultCount.Text=$"{items.Length} / {_all.Count} completed encounters";EmptyMessage.Visibility=items.Length==0?Visibility.Visible:Visibility.Collapsed;OpenButton.IsEnabled=items.Length>0;ExportButton.IsEnabled=items.Length>0;DeleteButton.IsEnabled=items.Length>0&&_delete is not null;ClearHistoryButton.IsEnabled=_clear is not null;UpdateBaselines();
    }
    private void Selection_Changed(object sender,SelectionChangedEventArgs e){if(Baseline is not null)UpdateBaselines();}
    private void UpdateBaselines()
    {
        var chosen=Fights.SelectedItem as Item;var old=(Baseline.SelectedItem as Item)?.Fight.Id;_changing=true;
        try{var items=chosen is null?[]:_all.Where(f=>f.Id!=chosen.Fight.Id&&EncounterReview.SameTarget(f,chosen.Fight)).Select(Row).ToArray();Baseline.ItemsSource=items;Baseline.SelectedItem=items.FirstOrDefault(i=>i.Fight.Id==old)??items.FirstOrDefault();Baseline.IsEnabled=items.Length>0;}
        finally{_changing=false;}Compare();
    }
    private void Baseline_Changed(object sender,SelectionChangedEventArgs e){if(!_changing&&ComparisonSummary is not null)Compare();}
    private void Compare()
    {
        if(Fights.SelectedItem is not Item chosen||Baseline.SelectedItem is not Item baseline){ComparisonSummary.Text="Select a completed encounter. A second fight against the same target is needed for comparison.";return;}
        var c=EncounterReview.Compare(chosen.Fight,baseline.Fight);if(!c.Comparable){ComparisonSummary.Text=c.Reason;return;}
        string Percent(double? value)=>value is {} n?n.ToString("+0.0%;-0.0%;0.0%"):"—";
        ComparisonSummary.Text=$"Party eDPS {Percent(c.PartyDpsChange)}  ·  Duration {Percent(c.DurationChange)}  ·  Your eDPS {Percent(c.SelfDpsChange)}\n"+c.Reason;
    }
    internal void ExportSelected(string path){if(Fights.SelectedItem is Item item)AscendAPP.Core.Storage.EncounterHistoryStore.Export(path,item.Fight);}
    private void Export_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new Microsoft.Win32.SaveFileDialog{Filter="Encounter archive (*.json)|*.json",FileName="ascendapp-encounter.json"};if(dialog.ShowDialog(this)!=true)return;
        try{ExportSelected(dialog.FileName);}catch(Exception ex)when(ex is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException){MessageBox.Show(this,ex.Message,"Could not export encounter");}
    }
    private void Delete_Click(object sender,RoutedEventArgs e)
    {
        if(Fights.SelectedItem is not Item item||_delete is null||!_confirm("Delete this saved encounter from this PC?"))return;
        if(_delete(item.Fight.Id)){_all.RemoveAll(f=>f.Id==item.Fight.Id);Filter();}else HistoryStorage.Text="History could not be changed. Check diagnostics.";
    }
    private void Clear_Click(object sender,RoutedEventArgs e)
    {
        if(_clear is null||!_confirm("Delete all saved encounters from this PC? Current combat and event timers are kept."))return;
        if(_clear()){_all.Clear();Filter();HistoryStorage.Text="Saved history cleared";}else HistoryStorage.Text="History could not be changed. Check diagnostics.";
    }
    private void Open_Click(object sender,RoutedEventArgs e)
    {if(Fights.SelectedItem is Item i)new SkillBreakdownWindow(i.Fight,hideNames:_hideNames){Owner=this}.ShowDialog();}
}
