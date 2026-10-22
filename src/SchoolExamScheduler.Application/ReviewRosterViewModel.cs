using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
namespace SchoolExamScheduler.Application;
public sealed record DutyRow(Guid Id,Guid TeacherId,DateOnly Date,string Teacher,string Examination,string Venue,int Position,TimeOnly Start,TimeOnly End,bool IsLocked,int Minutes)
{public string Label=>$"{Date:dd MMM} {Start:HH:mm} {Venue} P{Position} {Teacher}";}
public partial class ReviewRosterViewModel(ISchoolDataStore store,ManualRosterService service,RosterWorkflow workflow,IUserDialogs dialogs,ILogger<ReviewRosterViewModel> logger) : WorkspaceViewModel(logger)
{
    public ObservableCollection<RosterVersion> Rosters {get;}=[];
    public ObservableCollection<Teacher> Teachers {get;}=[];
    public ObservableCollection<DutyRow> Duties {get;}=[];
    public ObservableCollection<SchedulingIssue> Issues {get;}=[];
    [ObservableProperty] private RosterVersion? selectedRoster;
    [ObservableProperty] private DutyRow? selectedDuty;
    [ObservableProperty] private DutyRow? secondDuty;
    [ObservableProperty] private Teacher? replacementTeacher;
    [ObservableProperty] private string handover="09:00";
    [ObservableProperty] private string validationStatus="Select a roster and load its duties.";
    [ObservableProperty] private string search="";
    public IEnumerable<DutyRow> FilteredDuties=>Duties.Where(x=>$"{x.Teacher} {x.Examination} {x.Venue} {x.Date}".Contains(Search,StringComparison.CurrentCultureIgnoreCase)).OrderBy(x=>x.Date).ThenBy(x=>x.Start).ThenBy(x=>x.Venue).ThenBy(x=>x.Position);
    partial void OnSearchChanged(string value)=>OnPropertyChanged(nameof(FilteredDuties));
    private Guid Id=>SelectedRoster?.Id??throw new ArgumentException("Select a roster version.");
    private Guid DutyId=>SelectedDuty?.Id??throw new ArgumentException("Select a duty.");
    private Guid TeacherId=>ReplacementTeacher?.Id??throw new ArgumentException("Select a replacement teacher.");
    [RelayCommand] private Task LoadAsync()=>RunAsync(async()=>
    {var selected=SelectedRoster?.Id;Rosters.Clear();foreach(var row in (await store.ListAsync<RosterVersion>()).OrderByDescending(x=>x.CreatedOn))Rosters.Add(row);SelectedRoster=Rosters.FirstOrDefault(x=>x.Id==selected)??Rosters.FirstOrDefault();Teachers.Clear();foreach(var row in await store.ListAsync<Teacher>())Teachers.Add(row);});
    [RelayCommand] private Task LoadDutiesAsync()=>RunAsync(RefreshDutiesAsync);
    private async Task RefreshDutiesAsync()
    {
        var id=Id;var request=await workflow.LoadRequestAsync(id);Duties.Clear();
        foreach(var a in (await store.ListAsync<InvigilationAssignment>()).Where(x=>x.RosterVersionId==id && x.AssignmentStatus==AssignmentStatus.Assigned))
        {
            var allocation=request.ExamVenues.Single(x=>x.Id==a.ExamVenueId);var exam=request.Exams.Single(x=>x.Id==allocation.ExamId);var teacher=request.Teachers.Single(x=>x.Id==a.TeacherId);
            Duties.Add(new(a.Id,a.TeacherId,exam.ExamDate,$"{teacher.FirstName} {teacher.LastName}",$"Grade {exam.Grade} {request.Subjects.Single(x=>x.Id==exam.SubjectId).Name}",request.Venues.Single(x=>x.Id==allocation.VenueId).Name,a.PositionIndex+1,a.StartTime,a.EndTime,a.IsLocked,a.Interval.Minutes));
        }
        OnPropertyChanged(nameof(FilteredDuties));Issues.Clear();foreach(var issue in await service.ValidateAsync(id))Issues.Add(issue);ValidationStatus=Issues.Count==0?"Complete roster: hard-constraint validation passed.":"Incomplete or invalid roster — review issues before finalization.";
    }
    [RelayCommand] private Task ReplaceAsync()=>RunAsync(async()=>{await service.ReplaceAsync(Id,DutyId,TeacherId);await RefreshDutiesAsync();});
    [RelayCommand] private Task SwapAsync()=>RunAsync(async()=>{await service.SwapAsync(Id,DutyId,SecondDuty?.Id??throw new ArgumentException("Select a second duty."));await RefreshDutiesAsync();});
    [RelayCommand] private Task SplitAsync()=>RunAsync(async()=>{await service.SplitAsync(Id,DutyId,FormTimes.Parse(Handover),TeacherId);await RefreshDutiesAsync();});
    [RelayCommand] private Task CombineAsync()=>RunAsync(async()=>{await service.CombineAsync(Id,DutyId,SecondDuty?.Id??throw new ArgumentException("Select a second duty."));await RefreshDutiesAsync();});
    [RelayCommand] private Task RemoveAsync()=>RunAsync(async()=>{if(await dialogs.ConfirmAsync("Remove this duty from the draft? It will leave incomplete coverage.")){await service.RemoveAsync(Id,DutyId);await RefreshDutiesAsync();}});
    [RelayCommand] private Task ToggleLockAsync()=>RunAsync(async()=>{await service.SetLockAsync(Id,DutyId,!SelectedDuty!.IsLocked);await RefreshDutiesAsync();});
    [RelayCommand] private Task RegenerateAsync()=>RunAsync(async()=>{var result=await workflow.GenerateAsync(SelectedRoster!.Name+" regenerated",Id);if(result.Status is not(SchedulingStatus.Optimal or SchedulingStatus.Feasible))throw new InvalidOperationException(string.Join(Environment.NewLine,result.Issues.Select(x=>x.Message)));await LoadCommand.ExecuteAsync(null);SelectedRoster=Rosters.Single(x=>x.Id==workflow.LastCreatedRosterId);await RefreshDutiesAsync();});
    [RelayCommand] private Task FinalizeAsync()=>RunAsync(async()=>{if(await dialogs.ConfirmAsync("Finalize this roster? It will become read-only.")){await service.FinalizeAsync(Id);await LoadCommand.ExecuteAsync(null);await RefreshDutiesAsync();}});
}
