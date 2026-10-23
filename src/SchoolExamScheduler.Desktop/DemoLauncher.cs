using System.Diagnostics;
using SchoolExamScheduler.Application;
namespace SchoolExamScheduler.Desktop;
public sealed class DemoLauncher : IDemoLauncher
{
    public Task OpenAsync()
    {
        var executable=System.IO.Path.Combine(AppContext.BaseDirectory,"SchoolExamScheduler.Desktop.exe");
        if(!System.IO.File.Exists(executable))throw new InvalidOperationException("Start the Windows Desktop executable to open the separate demonstration school.");
        Process.Start(new ProcessStartInfo(executable){Arguments="--demo",UseShellExecute=true});
        return Task.CompletedTask;
    }
}
