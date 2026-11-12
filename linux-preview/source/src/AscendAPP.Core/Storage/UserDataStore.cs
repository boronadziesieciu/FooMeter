using System.Diagnostics;
using System.Text.Json;
using AscendAPP.Core.Settings;
namespace AscendAPP.Core.Storage;

/// <summary>One user-owned profile across releases. Legacy discovery runs only when no
/// valid shared settings exist; it never follows directory links or scans the whole drive.</summary>
public sealed class UserDataStore
{
    public string DirectoryPath {get;}
    public string SettingsPath=>Path.Combine(DirectoryPath,"settings.json");
    public string PetsPath=>Path.Combine(DirectoryPath,"pets.json");
    public string HistoryPath=>Path.Combine(DirectoryPath,"history.json");
    public string CharactersPath=>Path.Combine(DirectoryPath,"characters.json");
    public string FieldBossesPath=>Path.Combine(DirectoryPath,"fieldbosses.json");
    public string? ImportedFrom {get;private set;}
    public List<string> Messages {get;}=[];
    public static string DefaultDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AscendAPP");
    private UserDataStore(string directory)=>DirectoryPath=Path.GetFullPath(directory);
    // Preview/self-tests can describe the real path without reading or creating that profile.
    public static UserDataStore Preview()=>new(DefaultDirectory);
    public static UserDataStore OpenDefault(string applicationDirectory)=>
        Open(DefaultDirectory,applicationDirectory,()=>DefaultCandidates(applicationDirectory));

    public static UserDataStore Open(string directory,string applicationDirectory,Func<IEnumerable<string>>? additionalCandidates=null)
    {
        var store=new UserDataStore(directory);
        try
        {
            Directory.CreateDirectory(store.DirectoryPath);
            if(PreferencesStore.TryRead(store.SettingsPath,out _))return store;
            if(File.Exists(store.SettingsPath))
            {
                store.Backup(store.SettingsPath);
                store.Messages.Add("Invalid shared settings were backed up before recovery.");
            }
            var candidates=new[]{applicationDirectory}.Concat(additionalCandidates?.Invoke()??[]);
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var candidate in candidates)
            {
                if(string.IsNullOrWhiteSpace(candidate))continue;
                string full;
                try{full=Path.GetFullPath(candidate);}catch(Exception e)when(e is ArgumentException or NotSupportedException){continue;}
                if(full.Equals(store.DirectoryPath,StringComparison.OrdinalIgnoreCase)||!seen.Add(full))continue;
                if(!ReadSettings(Path.Combine(full,"settings.json"),out var text))continue;
                // Another launch may have created a valid profile during discovery.
                if(PreferencesStore.TryRead(store.SettingsPath,out _))return store;
                store.ImportOptional(full,"characters.json",4_000_000);
                store.ImportOptional(full,"fieldbosses.json",200_000);
                // Commit settings last; a partial first launch can safely retry missing files.
                AtomicFile.WriteAllText(store.SettingsPath,text,overwrite:File.Exists(store.SettingsPath));
                store.ImportedFrom=full;store.Messages.Add("Imported saved settings from "+full);
                break;
            }
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException)
        {store.Messages.Add("Could not prepare shared settings: "+e.Message);}
        return store;
    }
    private static bool ReadSettings(string path,out string text)
    {
        text="";
        try
        {
            if(new FileInfo(path).Length>1_048_576)return false;
            text=File.ReadAllText(path);return PreferencesStore.TryParse(text,out _);
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException){return false;}
    }
    private void ImportOptional(string source,string name,long limit)
    {
        string destination=Path.Combine(DirectoryPath,name),path=Path.Combine(source,name);
        if(File.Exists(destination)||!File.Exists(path))return;
        try
        {
            if(new FileInfo(path).Length>limit){Messages.Add(name+" was too large to import.");return;}
            string text=File.ReadAllText(path);using var doc=JsonDocument.Parse(text);
            if(doc.RootElement.ValueKind!=JsonValueKind.Array){Messages.Add(name+" was invalid and was left in the old folder.");return;}
            AtomicFile.WriteAllText(destination,text,overwrite:false);
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException)
        {Messages.Add("Could not import "+name+": "+e.Message);}
    }
    private string Backup(string path)
    {
        string backup=path+".backup-"+DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N");
        File.Copy(path,backup,overwrite:false);return backup;
    }
    public string ImportSettings(string path)
    {
        if(Path.GetFullPath(path).Equals(SettingsPath,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("This file is already the shared settings file.");
        if(!ReadSettings(path,out var text))throw new InvalidDataException("The selected file is not valid AscendAPP settings.");
        Directory.CreateDirectory(DirectoryPath);
        string? backup=File.Exists(SettingsPath)?Backup(SettingsPath):null;
        AtomicFile.WriteAllText(SettingsPath,text);
        ImportedFrom=Path.GetDirectoryName(Path.GetFullPath(path));
        return backup is null?"Settings imported.":"Settings imported. Previous settings were backed up in the shared data folder.";
    }
    private static IEnumerable<string> DefaultCandidates(string applicationDirectory)
    {
        // A running older copy is a stronger signal than arbitrary stale extraction folders.
        var running=new List<string>();
        foreach(var process in Process.GetProcessesByName("AscendAPP"))
        {
            using(process)try
            {
                var executable=process.MainModule?.FileName;
                if(executable is not null&&process.Id!=Environment.ProcessId)running.Add(Path.GetDirectoryName(executable)!);
            }catch(Exception e)when(e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){}
        }
        foreach(var folder in OrderByRecency(running))yield return folder;
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roots=new[]{
            Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationDirectory)))??Path.GetFullPath(applicationDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads")};
        var discovered=DiscoverLegacyFolders(roots).Append(Path.Combine(local,"Programs","AscendAPP"));
        foreach(var folder in OrderByRecency(discovered))yield return folder;
    }
    public static IReadOnlyList<string> DiscoverLegacyFolders(IEnumerable<string> roots,int maxDepth=3,int maxDirectories=1000)
    {
        var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue=new Queue<(string Folder,int Depth,bool Scoped)>();
        foreach(var root in roots)if(!string.IsNullOrWhiteSpace(root))queue.Enqueue((root,0,Path.GetFileName(Path.TrimEndingDirectorySeparator(root)).Contains("AscendAPP",StringComparison.OrdinalIgnoreCase)));
        int budget=Math.Clamp(maxDirectories,1,5000),depthLimit=Math.Clamp(maxDepth,1,6);
        while(queue.Count>0&&visited.Count<budget)
        {
            var item=queue.Dequeue();
            try
            {
                string full=Path.GetFullPath(item.Folder);
                if(!visited.Add(full)||!System.IO.Directory.Exists(full))continue;
                if((File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)continue;
                if(item.Scoped&&File.Exists(Path.Combine(full,"AscendAPP.exe"))&&File.Exists(Path.Combine(full,"settings.json")))found.Add(full);
                if(item.Depth>=depthLimit)continue;
                foreach(var child in System.IO.Directory.GetDirectories(full))
                {
                    bool scoped=item.Scoped||Path.GetFileName(child).Contains("AscendAPP",StringComparison.OrdinalIgnoreCase);
                    if(scoped&&queue.Count<budget)queue.Enqueue((child,item.Depth+1,true));
                }
            }catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){}
        }
        return found.ToArray();
    }
    public static IReadOnlyList<string> OrderByRecency(IEnumerable<string> candidates)
    {
        var valid=new List<(string Folder,DateTime At)>();
        foreach(var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try{var path=Path.Combine(candidate,"settings.json");if(File.Exists(path))valid.Add((candidate,File.GetLastWriteTimeUtc(path)));}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){}
        }
        return valid.OrderByDescending(x=>x.At).ThenBy(x=>x.Folder,StringComparer.OrdinalIgnoreCase).Select(x=>x.Folder).ToArray();
    }
}
