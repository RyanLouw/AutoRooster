using System.Text.Json;
using SchoolExamScheduler.Licensing;
namespace SchoolExamScheduler.Infrastructure;

// Separate from school backups: restoring a school database must not restore an old licence lease.
public sealed class FileActivationStore(string path):IActivationStore
{
    public async Task<ActivationData> ReadAsync(CancellationToken token=default)
    {
        if(File.Exists(path))
        {
            try
            {
                var data=JsonSerializer.Deserialize<ActivationData>(await File.ReadAllTextAsync(path,token));
                if(data is not null && data.InstallationId!=Guid.Empty)return data;
            }
            catch(JsonException){ /* A damaged cache requires fresh activation. */ }
        }
        var fresh=new ActivationData(Guid.NewGuid(),"",null,DateTimeOffset.MinValue);
        await SaveAsync(fresh,token);return fresh;
    }
    public async Task SaveAsync(ActivationData data,CancellationToken token=default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(data),token);
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(temporary,UnixFileMode.UserRead|UnixFileMode.UserWrite);
            File.Move(temporary,path,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
