using System.Text;
namespace AscendAPP.Core.Storage;
public static class AtomicFile
{
    // Bound lock storage while serializing writes to the same Windows path.
    private static readonly object[] Gates=Enumerable.Range(0,64).Select(_=>new object()).ToArray();
    public static void WriteAllText(string path,string content,bool overwrite=true,bool flushToDisk=false)
    {
        string full=Path.GetFullPath(path);
        var gate=Gates[(uint)StringComparer.OrdinalIgnoreCase.GetHashCode(full)%Gates.Length];
        lock(gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            string temporary=full+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=new UTF8Encoding(false).GetBytes(content);stream.Write(bytes);stream.Flush(flushToDisk);}
                for(int attempt=0;;attempt++)
                {
                    try{File.Move(temporary,full,overwrite);break;}
                    catch(Exception e)when((e is IOException or UnauthorizedAccessException)&&attempt<3&&
                        !Directory.Exists(full)&&(overwrite||!File.Exists(full)))
                    {Thread.Sleep(10<<attempt);}
                }
            }
            finally{try{File.Delete(temporary);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}}
        }
    }
}
