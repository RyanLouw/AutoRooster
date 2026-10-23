using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Application;

public partial class TeacherWorkspaceViewModel(ISchoolDataStore store,IUserDialogs dialogs,ILogger<TeacherWorkspaceViewModel> logger) : WorkspaceViewModel(logger)
{
    public override Task RefreshAsync()=>LoadCommand.IsRunning ? LoadCommand.ExecutionTask??Task.CompletedTask : LoadCommand.ExecuteAsync(null);
    public ObservableCollection<Teacher> Teachers {get;}=[];
    public IEnumerable<Teacher> FilteredTeachers=>Teachers.Where(x=>!ActiveOnly || x.IsActive).Where(x=>$"{x.FirstName} {x.LastName} {x.EmployeeNumber}".Contains(Search,StringComparison.CurrentCultureIgnoreCase)).OrderBy(x=>x.LastName).ThenBy(x=>x.FirstName);
    [ObservableProperty] private string search="";
    [ObservableProperty] private bool activeOnly;
    [ObservableProperty] private Teacher? selectedTeacher;
    [ObservableProperty] private string firstName="";
    [ObservableProperty] private string lastName="";
    [ObservableProperty] private string? employeeNumber;
    [ObservableProperty] private string? email;
    [ObservableProperty] private decimal workloadWeight=1m;
    [ObservableProperty] private int? maximumDailyMinutes;
    [ObservableProperty] private int? maximumPeriodMinutes;
    [ObservableProperty] private bool isActive=true;
    [ObservableProperty] private bool canInvigilate=true;
    [ObservableProperty] private string notes="";
    partial void OnSearchChanged(string value)=>OnPropertyChanged(nameof(FilteredTeachers));
    partial void OnActiveOnlyChanged(bool value)=>OnPropertyChanged(nameof(FilteredTeachers));
    partial void OnSelectedTeacherChanged(Teacher? value)
    {
        if(value is null)return;
        FirstName=value.FirstName;LastName=value.LastName;EmployeeNumber=value.EmployeeNumber;Email=value.Email;
        WorkloadWeight=value.WorkloadWeight;MaximumDailyMinutes=value.MaximumDailyMinutes;MaximumPeriodMinutes=value.MaximumPeriodMinutes;
        IsActive=value.IsActive;CanInvigilate=value.CanInvigilate;Notes=value.Notes;
    }
    [RelayCommand] private Task LoadAsync()=>RunAsync(async()=>
    {
        var rows=await store.ListAsync<Teacher>();Teachers.Clear();foreach(var row in rows)Teachers.Add(row);OnPropertyChanged(nameof(FilteredTeachers));
    });
    [RelayCommand] private void New()
    {SelectedTeacher=null;FirstName="";LastName="";EmployeeNumber=null;Email=null;WorkloadWeight=1;MaximumDailyMinutes=null;MaximumPeriodMinutes=null;IsActive=true;CanInvigilate=true;Notes="";Error="";}
    [RelayCommand] private Task SaveAsync()=>RunAsync(async()=>
    {
        // Validate the complete form before mutating an existing entity.
        var validated=new Teacher(FirstName,LastName,WorkloadWeight){MaximumDailyMinutes=MaximumDailyMinutes,MaximumPeriodMinutes=MaximumPeriodMinutes};
        if(!string.IsNullOrWhiteSpace(Email) && !System.Net.Mail.MailAddress.TryCreate(Email,out _))throw new ArgumentException("Enter a valid email address or leave it empty.");
        var teacher=SelectedTeacher??validated;
        teacher.FirstName=validated.FirstName;teacher.LastName=validated.LastName;teacher.WorkloadWeight=validated.WorkloadWeight;
        teacher.MaximumDailyMinutes=validated.MaximumDailyMinutes;teacher.MaximumPeriodMinutes=validated.MaximumPeriodMinutes;
        teacher.EmployeeNumber=string.IsNullOrWhiteSpace(EmployeeNumber)?null:EmployeeNumber.Trim();teacher.Email=Email;teacher.IsActive=IsActive;teacher.CanInvigilate=CanInvigilate;teacher.Notes=Notes;
        await store.SaveAsync(teacher);if(!Teachers.Contains(teacher))Teachers.Add(teacher);SelectedTeacher=teacher;OnPropertyChanged(nameof(FilteredTeachers));
    });
    [RelayCommand] private Task DeactivateAsync()=>RunAsync(async()=>
    {
        if(SelectedTeacher is null)throw new ArgumentException("Select a teacher first.");
        if(!await dialogs.ConfirmAsync("Deactivate the selected teacher? Historical duties will be retained."))return;
        SelectedTeacher.IsActive=false;await store.SaveAsync(SelectedTeacher);IsActive=false;OnPropertyChanged(nameof(FilteredTeachers));
    });
}
