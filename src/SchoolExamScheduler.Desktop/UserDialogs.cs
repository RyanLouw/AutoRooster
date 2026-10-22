using System.Windows;
using SchoolExamScheduler.Application;
namespace SchoolExamScheduler.Desktop;
public sealed class UserDialogs : IUserDialogs
{
    public Task<bool> ConfirmAsync(string message)=>Task.FromResult(MessageBox.Show(message,"Confirm change",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes);
}
