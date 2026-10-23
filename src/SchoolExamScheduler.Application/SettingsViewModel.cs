using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Application;
public partial class SettingsViewModel(ISchoolDataStore store,IDatabaseMaintenance maintenance,IFileDialogs files,IUserDialogs dialogs,ILogger<SettingsViewModel> logger,IDemoLauncher? demoLauncher=null) : WorkspaceViewModel(logger)
{
    public override Task RefreshAsync()=>LoadCommand.IsRunning ? LoadCommand.ExecutionTask??Task.CompletedTask : LoadCommand.ExecuteAsync(null);
    [ObservableProperty] private SchedulingSettings settings=new();
    [ObservableProperty] private string dayStart="07:00";
    [ObservableProperty] private string dayEnd="17:00";
    [ObservableProperty] private string status="";
    public event Action? DatabaseRestored;
    [RelayCommand] private Task LoadAsync()=>RunAsync(async()=>{Settings=(await store.ListAsync<SchedulingSettings>()).Single();DayStart=Settings.DefaultDayStartTime.ToString("HH:mm");DayEnd=Settings.DefaultDayEndTime.ToString("HH:mm");});
    [RelayCommand] private Task SaveAsync()=>RunAsync(async()=>{var interval=new TimeInterval(FormTimes.Parse(DayStart),FormTimes.Parse(DayEnd));Settings.DefaultDayStartTime=interval.Start;Settings.DefaultDayEndTime=interval.End;Settings.Validate();await store.SaveAsync(Settings);Status="Settings saved.";});
    [RelayCommand] private Task EnableThirtyMinuteHandoversAsync()=>RunAsync(async()=>
    {
        if(!await dialogs.ConfirmAsync("Enable handovers of 30 minutes or longer in every grade, including Grades 8 and 9? Existing rosters stay unchanged. Generate a new roster version to use these settings."))return;
        Settings.AllowSessionSplitting=true;Settings.MinimumSessionMinutes=30;Settings.SchedulingTimeIncrementMinutes=30;
        Settings.MaximumSessionSegmentsPerPosition=Math.Max(3,Settings.MaximumSessionSegmentsPerPosition);
        Settings.Validate();await store.SaveAsync(Settings);
        OnPropertyChanged(nameof(Settings));Status="30-minute handovers enabled for all grades. Generate a new roster version; existing exports will not change.";
    });
    [RelayCommand] private Task BackupAsync()=>RunAsync(async()=>{var path=await files.SavePathAsync("Create a new database backup","SQLite backup|*.db",".db");if(path is null)return;await maintenance.BackupAsync(path);Status="Backup created and integrity checked.";});
    [RelayCommand] private Task RestoreAsync()=>RunAsync(async()=>
    {
        var path=await files.OpenPathAsync("Choose a scheduler database backup","SQLite backup|*.db");if(path is null)return;
        if(!await dialogs.ConfirmAsync("Restore this backup and replace the current database? A recovery copy will be retained. The application will close after restore; reopen it to load the restored data."))return;
        await maintenance.RestoreAsync(path,true);Status="Database restored. Reopen the application.";DatabaseRestored?.Invoke();
    });
    [RelayCommand] private Task OpenRealisticDemoAsync()=>RunAsync(async()=>
    {
        if(demoLauncher is null)throw new InvalidOperationException("The demo launcher is only available in the Desktop application.");
        await demoLauncher.OpenAsync();Status="Opened the realistic test school in a separate window and database.";
    });
    [RelayCommand] private Task SampleDataAsync()=>RunAsync(async()=>
    {
        if((await store.ListAsync<Teacher>()).Count>0 || (await store.ListAsync<Exam>()).Count>0 || (await store.ListAsync<Subject>()).Count>0 || (await store.ListAsync<Venue>()).Count>0)throw new InvalidOperationException("Sample data can only be added to an empty school database. Preserve your real data.");
        if(!await dialogs.ConfirmAsync("Create a demonstration school with 75 teachers and ten examination dates? This also replaces the empty school's settings with sample defaults."))return;
        var sample=DemoDataFactory.Create(42,DateOnly.FromDateTime(DateTime.Today).AddDays(7));await store.CommitAsync(sample.Records,[]);Status="Sample school created. Open any tab to view it.";await LoadCommand.ExecuteAsync(null);
    });
}
