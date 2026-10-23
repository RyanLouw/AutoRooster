using System.IO;
using System.Text.Json;
using SchoolExamScheduler.Licensing;
namespace SchoolExamScheduler.Desktop;
internal static class LicenceConfiguration
{
    public static LicenceClientOptions Load()
    {
        var file=Path.Combine(AppContext.BaseDirectory,"licensing.json");
#if DEBUG
        var local=Path.Combine(AppContext.BaseDirectory,"licensing.local.json");
        if(File.Exists(local))file=local;
#endif
        if(!File.Exists(file))return new("","");
        var options=JsonSerializer.Deserialize<LicenceClientOptions>(File.ReadAllText(file),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new("","");
#if DEBUG
        return options;
#else
        return options with{AllowInsecureLoopback=false};
#endif
    }
}
