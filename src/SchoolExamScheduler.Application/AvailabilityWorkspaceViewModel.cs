using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Application;
public partial class AvailabilityWorkspaceViewModel(ISchoolDataStore store,IUserDialogs dialogs,ILogger<AvailabilityWorkspaceViewModel> logger) : WorkspaceViewModel(logger)
{
    public ObservableCollection<Teacher> Teachers {get;}=[];
    public ObservableCollection<TeacherAvailability> Records {get;}=[];
    public IEnumerable<TeacherAvailability> FilteredRecords=>Records.Where(x=>(SelectedTeacher is null || x.TeacherId==SelectedTeacher.Id) && (!SelectedDateOnly || x.Date==DateOnly.FromDateTime(Date))).OrderBy(x=>x.Date).ThenBy(x=>x.StartTime);
    [ObservableProperty] private Teacher? selectedTeacher;
    [ObservableProperty] private TeacherAvailability? selectedRecord;
    [ObservableProperty] private DateTime date=DateTime.Today;
    [ObservableProperty] private bool selectedDateOnly;
    [ObservableProperty] private string startTime="07:00";
    [ObservableProperty] private string endTime="17:00";
    [ObservableProperty] private bool isAvailable;
    [ObservableProperty] private string reason="";
    partial void OnSelectedTeacherChanged(Teacher? value)=>OnPropertyChanged(nameof(FilteredRecords));
    partial void OnDateChanged(DateTime value)=>OnPropertyChanged(nameof(FilteredRecords));
    partial void OnSelectedDateOnlyChanged(bool value)=>OnPropertyChanged(nameof(FilteredRecords));
    partial void OnSelectedRecordChanged(TeacherAvailability? value)
    {
        if(value is null)return;SelectedTeacher=Teachers.FirstOrDefault(x=>x.Id==value.TeacherId);Date=value.Date.ToDateTime(TimeOnly.MinValue);
        StartTime=value.StartTime.ToString("HH:mm");EndTime=value.EndTime.ToString("HH:mm");IsAvailable=value.IsAvailable;Reason=value.Reason;
    }
    [RelayCommand] private Task LoadAsync()=>RunAsync(async()=>
    {
        Teachers.Clear();foreach(var teacher in await store.ListAsync<Teacher>())Teachers.Add(teacher);
        Records.Clear();foreach(var record in await store.ListAsync<TeacherAvailability>())Records.Add(record);OnPropertyChanged(nameof(FilteredRecords));
    });
    [RelayCommand] private void New(){SelectedRecord=null;Reason="";Error="";}
    [RelayCommand] private void FullDay(){StartTime="00:00";EndTime="23:59";IsAvailable=false;}
    [RelayCommand] private Task SaveAsync()=>RunAsync(async()=>
    {
        if(SelectedTeacher is null)throw new ArgumentException("Select a teacher.");
        if(!TimeOnly.TryParseExact(StartTime,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var start)||!TimeOnly.TryParseExact(EndTime,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var end))throw new ArgumentException("Use 24-hour times such as 08:30.");
        var validated=new TeacherAvailability(SelectedTeacher.Id,DateOnly.FromDateTime(Date),start,end,IsAvailable){Reason=Reason};
        var record=SelectedRecord??validated;
        if(SelectedRecord is not null)record.Update(validated.TeacherId,validated.Date,start,end,IsAvailable,Reason);
        await store.SaveAsync(record);if(!Records.Contains(record))Records.Add(record);SelectedRecord=record;OnPropertyChanged(nameof(FilteredRecords));
    });
    [RelayCommand] private Task RemoveAsync()=>RunAsync(async()=>
    {
        if(SelectedRecord is null)throw new ArgumentException("Select an availability entry.");
        if(!await dialogs.ConfirmAsync("Remove the selected availability entry?"))return;
        await store.DeleteAsync(SelectedRecord);Records.Remove(SelectedRecord);SelectedRecord=null;OnPropertyChanged(nameof(FilteredRecords));
    });
}
