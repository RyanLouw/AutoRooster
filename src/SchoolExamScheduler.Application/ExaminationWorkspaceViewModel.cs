using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
namespace SchoolExamScheduler.Application;
public partial class VenueAllocationRow(Guid venueId,int studentCount,int invigilatorCount) : ObservableObject
{
    [ObservableProperty] private Guid venueId=venueId;
    [ObservableProperty] private int studentCount=studentCount;
    [ObservableProperty] private int invigilatorCount=invigilatorCount;
}
public partial class ExaminationWorkspaceViewModel(ISchoolDataStore store,ILogger<ExaminationWorkspaceViewModel> logger) : WorkspaceViewModel(logger)
{
    public override Task RefreshAsync()=>LoadCommand.IsRunning ? LoadCommand.ExecutionTask??Task.CompletedTask : LoadCommand.ExecuteAsync(null);
    public ObservableCollection<Exam> Exams {get;}=[];
    public ObservableCollection<Subject> Subjects {get;}=[];
    public ObservableCollection<Venue> Venues {get;}=[];
    public ObservableCollection<VenueAllocationRow> Allocations {get;}=[];
    private IReadOnlyList<ExamVenue> persistedAllocations=[];
    public IEnumerable<Exam> FilteredExams=>Exams.Where(x=>ShowPreviousSeries || x.IsActive).Where(x=>!DateOnlyFilter || x.ExamDate==DateOnly.FromDateTime(Date)).Where(x=>$"{x.Grade} {Subjects.FirstOrDefault(s=>s.Id==x.SubjectId)?.Name} {x.Description}".Contains(Search,StringComparison.CurrentCultureIgnoreCase)).OrderBy(x=>x.ExamDate).ThenBy(x=>x.StartTime);
    [ObservableProperty] private bool showPreviousSeries;
    partial void OnShowPreviousSeriesChanged(bool value)=>OnPropertyChanged(nameof(FilteredExams));
    [ObservableProperty] private string search="";
    [ObservableProperty] private bool dateOnlyFilter;
    [ObservableProperty] private Exam? selectedExam;
    [ObservableProperty] private Subject? selectedSubject;
    [ObservableProperty] private Venue? selectedVenue;
    [ObservableProperty] private VenueAllocationRow? selectedAllocation;
    [ObservableProperty] private int grade=8;
    [ObservableProperty] private DateTime date=DateTime.Today;
    [ObservableProperty] private string startTime="08:00";
    [ObservableProperty] private string endTime="10:00";
    [ObservableProperty] private string description="";
    [ObservableProperty] private bool examActive=true;
    partial void OnSearchChanged(string value)=>OnPropertyChanged(nameof(FilteredExams));
    partial void OnDateOnlyFilterChanged(bool value)=>OnPropertyChanged(nameof(FilteredExams));
    partial void OnDateChanged(DateTime value)=>OnPropertyChanged(nameof(FilteredExams));
    partial void OnSelectedExamChanged(Exam? value)
    {
        if(value is null)return;SelectedSubject=Subjects.FirstOrDefault(s=>s.Id==value.SubjectId);Grade=value.Grade;Date=value.ExamDate.ToDateTime(TimeOnly.MinValue);StartTime=value.StartTime.ToString("HH:mm");EndTime=value.EndTime.ToString("HH:mm");Description=value.Description;ExamActive=value.IsActive;
        Allocations.Clear();foreach(var row in persistedAllocations.Where(x=>x.ExamId==value.Id))Allocations.Add(new(row.VenueId,row.ExpectedStudentCount,row.RequiredInvigilatorCount));
    }
    [RelayCommand] private Task LoadAsync()=>RunAsync(async()=>
    {
        Subjects.Clear();foreach(var row in await store.ListAsync<Subject>())Subjects.Add(row);
        Venues.Clear();foreach(var row in await store.ListAsync<Venue>())Venues.Add(row);
        persistedAllocations=await store.ListAsync<ExamVenue>();Exams.Clear();foreach(var row in await store.ListAsync<Exam>())Exams.Add(row);OnPropertyChanged(nameof(FilteredExams));
    });
    [RelayCommand] private void New(){SelectedExam=null;SelectedSubject=null;Grade=8;Description="";ExamActive=true;Allocations.Clear();}
    [RelayCommand] private void Copy(){SelectedExam=null;ExamActive=true;Description+=" (copy)";}
    [RelayCommand] private void AddVenue()
    {if(SelectedVenue is null){Error="Select a venue first.";return;}Allocations.Add(new(SelectedVenue.Id,SelectedVenue.Capacity,1));}
    [RelayCommand] private void RemoveVenue(){if(SelectedAllocation is not null)Allocations.Remove(SelectedAllocation);}
    [RelayCommand] private Task SaveAsync()=>RunAsync(async()=>
    {
        if(SelectedSubject is null)throw new ArgumentException("Select a subject.");
        var valid=new Exam(SelectedSubject.Id,Grade,DateOnly.FromDateTime(Date),FormTimes.Parse(StartTime),FormTimes.Parse(EndTime)){Description=Description,IsActive=ExamActive};
        if(Allocations.Count==0 || Allocations.Select(x=>x.VenueId).Distinct().Count()!=Allocations.Count)throw new ArgumentException("Add at least one venue, without duplicate venues.");
        var old=persistedAllocations.Where(x=>x.ExamId==SelectedExam?.Id).ToArray();
        if((await store.ListAsync<InvigilationAssignment>()).Any(x=>old.Any(v=>v.Id==x.ExamVenueId)))throw new InvalidOperationException("This exam has historical roster duties. Copy it to preserve roster history.");
        var newAllocations=Allocations.Select(x=>new ExamVenue(valid.Id,x.VenueId,x.StudentCount,x.InvigilatorCount)).ToArray();
        var request=new SchedulingRequest { Teachers=[],Subjects=Subjects.ToArray(),Venues=Venues.ToArray(),Exams=[..Exams.Where(x=>x.Id!=SelectedExam?.Id),valid],ExamVenues=[..persistedAllocations.Where(x=>x.ExamId!=SelectedExam?.Id),..newAllocations],Configuration=(await store.ListAsync<SchedulingSettings>()).FirstOrDefault()??new(),VenueUnavailability=await store.ListAsync<VenueUnavailability>() };
        var issues=RequestValidator.Validate(request);if(issues.Count>0)throw new ArgumentException(string.Join(Environment.NewLine,issues.Select(x=>x.Message)));
        var exam=SelectedExam??valid;
        if(SelectedExam is not null)exam.Update(valid.SubjectId,valid.Grade,valid.ExamDate,valid.StartTime,valid.EndTime);
        exam.Description=Description;exam.IsActive=ExamActive;
        var allocations=Allocations.Select(x=>new ExamVenue(exam.Id,x.VenueId,x.StudentCount,x.InvigilatorCount)).ToArray();
        await store.CommitAsync([exam,..allocations],old.Cast<object>().ToArray());
        persistedAllocations=[..persistedAllocations.Except(old),..allocations];if(!Exams.Contains(exam))Exams.Add(exam);SelectedExam=exam;OnPropertyChanged(nameof(FilteredExams));
    });
}
