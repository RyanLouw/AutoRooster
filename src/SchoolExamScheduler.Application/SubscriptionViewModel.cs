using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Licensing;
namespace SchoolExamScheduler.Application;
public partial class SubscriptionViewModel(ILicenceService licence,ILogger<SubscriptionViewModel> logger):WorkspaceViewModel(logger)
{
    [ObservableProperty] private string licenceKey="";
    [ObservableProperty] private LicenceState state=new(LicenceStatus.NotActivated,"Checking subscription…");
    public string ServerAddress=>licence.ServerAddress;
    public bool CanWrite=>State.CanWrite;
    public string PaidThrough=>State.PaidUntilUtc is {} end ? LicenceDates.PaidThrough(end).ToString("dd MMM yyyy"):"—";
    public string OfflineUntil=>State.OfflineUntilUtc?.ToLocalTime().ToString("dd MMM yyyy HH:mm")??"—";
    partial void OnStateChanged(LicenceState value){OnPropertyChanged(nameof(CanWrite));OnPropertyChanged(nameof(PaidThrough));OnPropertyChanged(nameof(OfflineUntil));}
    public override Task RefreshAsync()=>RefreshCommand.ExecuteAsync(null);
    private async Task ApplyAsync(Func<Task<LicenceState>> action)
    {
        // Fail closed in the UI too if the local cache cannot be read or written.
        State=new(LicenceStatus.Unavailable,"Checking subscription…");
        await RunAsync(async()=>State=await action());
        if(Error.Length>0)State=new(LicenceStatus.Unavailable,"Licence check failed. Check the connection and refresh.");
    }
    [RelayCommand] private Task Refresh()=>ApplyAsync(()=>licence.CheckAsync());
    [RelayCommand] private async Task ActivateAsync(){await ApplyAsync(()=>licence.ActivateAsync(LicenceKey));LicenceKey="";}
}
