using Microsoft.Win32;
using SchoolExamScheduler.Application;
namespace SchoolExamScheduler.Desktop;
public sealed class FileDialogs : IFileDialogs
{
    public Task<string?> SavePathAsync(string title,string filter,string extension)
    {
        var dialog=new SaveFileDialog {Title=title,Filter=filter,DefaultExt=extension,AddExtension=true,OverwritePrompt=false};
        return Task.FromResult(dialog.ShowDialog()==true?dialog.FileName:null);
    }
    public Task<string?> OpenPathAsync(string title,string filter)
    {
        var dialog=new OpenFileDialog {Title=title,Filter=filter,CheckFileExists=true};
        return Task.FromResult(dialog.ShowDialog()==true?dialog.FileName:null);
    }
}
