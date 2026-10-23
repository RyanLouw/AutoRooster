using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Application;
public partial class SettingsViewModel(ISchoolDataStore store,IDatabaseMaintenance maintenance,IFileDialogs files,IUserDialogs dialogs,ILogger<SettingsViewModel> logger,IDemoLauncher? demoLauncher=null,ExamSeriesService? seriesService=null) : WorkspaceViewModel(logger)
{
    public override Task RefreshAsync()=>LoadCommand.IsRunning ? LoadCommand.ExecutionTask??Task.CompletedTask : LoadCommand.ExecuteAsync(null);
    [ObservableProperty] private SchedulingSettings settings=new();
    [ObservableProperty] private string dayStart="07:00";
    [ObservableProperty] private string dayEnd="17:00";
    [ObservableProperty] private string status="";
    public ObservableCollection<NextSeriesTeacherRow> NextSeriesTeachers {get;}=[];
    private bool staffSelectionLoaded;
    private async Task LoadNextSeriesTeachersAsync()
    {
        var choices=NextSeriesTeachers.ToDictionary(t=>t.TeacherId,t=>t.Keep);NextSeriesTeachers.Clear();
        foreach(var teacher in (await store.ListAsync<Teacher>()).Where(t=>t.IsActive).OrderBy(t=>t.LastName).ThenBy(t=>t.FirstName))
            NextSeriesTeachers.Add(new(teacher.Id,$"{teacher.FirstName} {teacher.LastName}",teacher.EmployeeNumber??"",choices.GetValueOrDefault(teacher.Id,true)));
        staffSelectionLoaded=true;
    }
    public event Action? DatabaseRestored;
    public event Action? ExamSeriesStarted;
    [RelayCommand] private Task LoadAsync()=>RunAsync(async()=>{Settings=(await store.ListAsync<SchedulingSettings>()).Single();DayStart=Settings.DefaultDayStartTime.ToString("HH:mm");DayEnd=Settings.DefaultDayEndTime.ToString("HH:mm");await LoadNextSeriesTeachersAsync();});
    [RelayCommand] private Task SaveAsync()=>RunAsync(async()=>{var interval=new TimeInterval(FormTimes.Parse(DayStart),FormTimes.Parse(DayEnd));Settings.DefaultDayStartTime=interval.Start;Settings.DefaultDayEndTime=interval.End;Settings.Validate();await store.SaveAsync(Settings);Status="Settings saved.";});
    [RelayCommand] private Task EnableThirtyMinuteHandoversAsync()=>RunAsync(async()=>
    {
        if(!await dialogs.ConfirmAsync("Enable handovers of 30 minutes or longer in every grade, including Grades 8 and 9? Existing rosters stay unchanged. Generate a new roster version to use these settings."))return;
        Settings.AllowSessionSplitting=true;Settings.MinimumSessionMinutes=30;Settings.SchedulingTimeIncrementMinutes=30;
        Settings.MaximumSessionSegmentsPerPosition=Math.Max(3,Settings.MaximumSessionSegmentsPerPosition);
        Settings.Validate();await store.SaveAsync(Settings);
        OnPropertyChanged(nameof(Settings));Status="30-minute handovers enabled for all grades. Generate a new roster version; existing exports will not change.";
    });
    [RelayCommand] private void KeepAllTeachers(){foreach(var teacher in NextSeriesTeachers)teacher.Keep=true;}
    [RelayCommand] private void KeepNoTeachers(){foreach(var teacher in NextSeriesTeachers)teacher.Keep=false;}
    [RelayCommand] private Task StartNextSeriesAsync()=>RunAsync(async()=>
    {
        if(!staffSelectionLoaded)await LoadNextSeriesTeachersAsync();
        var keep=NextSeriesTeachers.Where(t=>t.Keep).Select(t=>t.TeacherId).ToArray();
        if(!await dialogs.ConfirmAsync($"Start a new test series with {keep.Length} teachers? The old exams and rosters will leave the normal screens. {NextSeriesTeachers.Count-keep.Length} unticked teachers will leave the active staff list. Old results stay available under Show previous series. No previous duty hours carry over. Subjects, venues, settings and dated availability are kept."))return;
        if(seriesService is null)throw new InvalidOperationException("The exam-series service is unavailable.");
        var result=await seriesService.FinishAsync(keep);
        Status=$"New series ready: {result.ExamsArchived} old exams cleared from the working list; {keep.Length} teachers kept. Add the new exams in Examinations, update staff or availability as needed, then generate a new roster.";
        await LoadNextSeriesTeachersAsync();
        ExamSeriesStarted?.Invoke();
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

public partial class NextSeriesTeacherRow(Guid teacherId,string teacher,string employeeNumber,bool keep) : ObservableObject
{
    public Guid TeacherId {get;}=teacherId;
    public string Teacher {get;}=teacher;
    public string EmployeeNumber {get;}=employeeNumber;
    [ObservableProperty] private bool keep=keep;
}
