using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
namespace SchoolExamScheduler.Application;
public partial class GenerationWorkspaceViewModel(RosterWorkflow workflow,ILogger<GenerationWorkspaceViewModel> logger) : WorkspaceViewModel(logger)
{
    public override Task RefreshAsync()=>ValidateInputsCommand.IsRunning ? ValidateInputsCommand.ExecutionTask??Task.CompletedTask : ValidateInputsCommand.ExecuteAsync(null);
    [ObservableProperty] private string rosterName="Examination roster";
    [ObservableProperty] private Guid? lockedRosterId;
    [ObservableProperty] private string status="Validate inputs before generating.";
    [ObservableProperty] private string handoverPolicy="Validate inputs to see the active handover settings.";
    [ObservableProperty] private int examinationCount;
    [ObservableProperty] private int positionCount;
    [ObservableProperty] private long requiredMinutes;
    [ObservableProperty] private int eligibleTeachers;
    [ObservableProperty] private SchedulingResult? result;
    public double AverageHours=>EligibleTeachers>0?RequiredMinutes/60.0/EligibleTeachers:0;
    public ObservableCollection<RosterTargetRow> Targets {get;}=[];
    public ObservableCollection<SchedulingIssue> Issues {get;}=[];
    public ObservableCollection<TeacherWorkload> Workloads {get;}=[];
    private void ShowIssues(IEnumerable<SchedulingIssue> issues){Issues.Clear();foreach(var issue in issues)Issues.Add(issue);}
    [RelayCommand] private Task ValidateInputsAsync()=>RunAsync(async()=>
    {
        var request=await workflow.LoadRequestAsync(LockedRosterId);ExaminationCount=request.Exams.Count(x=>x.IsActive);
        var active=request.Exams.Where(x=>x.IsActive).ToDictionary(x=>x.Id);
        PositionCount=request.ExamVenues.Where(x=>active.ContainsKey(x.ExamId)).Sum(x=>x.RequiredInvigilatorCount);
        RequiredMinutes=request.ExamVenues.Where(x=>active.ContainsKey(x.ExamId)).Sum(x=>(long)x.RequiredInvigilatorCount*active[x.ExamId].Interval.Minutes);
        EligibleTeachers=request.Teachers.Count(x=>x.IsEligible);
        HandoverPolicy=request.Configuration.AllowSessionSplitting?$"Handovers enabled for every grade: minimum {request.Configuration.MinimumSessionMinutes} minutes per segment, up to {request.Configuration.MaximumSessionSegmentsPerPosition} segments per invigilator position.":"Handovers are disabled in the saved settings. To allow 30-minute sessions, open Settings and select Enable 30-minute handovers, then generate a new roster version.";
        var issues=RequestValidator.Validate(request);ShowIssues(issues);
        Status=issues.Count==0?"Inputs validated. Hard constraints remain mandatory during generation.":"Correct the input issues before generation.";
        if(issues.Count>0)Error=string.Join(Environment.NewLine,issues.Select(x=>x.Message));
        OnPropertyChanged(nameof(AverageHours));Targets.Clear();
        if(issues.Count==0)ShowTargets(request,FairnessCalculator.Calculate(request,[]));
    });
    [RelayCommand(IncludeCancelCommand=true)] private Task GenerateAsync(CancellationToken cancellationToken)=>RunAsync(async()=>
    {
        Status="Solving the complete examination period…";Workloads.Clear();
        Result=await workflow.GenerateAsync(RosterName,LockedRosterId,cancellationToken);Status=Result.Status.ToString();ShowIssues(Result.Issues);
        foreach(var workload in Result.Fairness.Teachers)Workloads.Add(workload);
        if(Result.Status is SchedulingStatus.Optimal or SchedulingStatus.Feasible)
        {
            ShowTargets(await workflow.LoadRequestAsync(LockedRosterId),Result.Fairness);
            Status=$"{Result.Status}: complete coverage; {Result.Fairness.UnusedEligibleTeachers} available teachers unused; largest difference from target {Result.Fairness.MaximumNormalizedDeviation:P1}.";
        }
        if(Result.Status is not (SchedulingStatus.Optimal or SchedulingStatus.Feasible or SchedulingStatus.Cancelled))Error=string.Join(Environment.NewLine,Result.Issues.Select(x=>x.Message));
    });
    private void ShowTargets(SchedulingRequest request,FairnessStatistics fairness)
    {
        Targets.Clear();var teachers=request.Teachers.ToDictionary(t=>t.Id);
        foreach(var row in fairness.Teachers.OrderBy(r=>teachers[r.TeacherId].LastName).ThenBy(r=>teachers[r.TeacherId].FirstName))
        {
            var teacher=teachers[row.TeacherId];Targets.Add(new(teacher.Id,$"{teacher.FirstName} {teacher.LastName}",teacher.EmployeeNumber??"",row.Weight,row.AvailableMinutes/60.0,row.TargetMinutes/60,row.ActualMinutes/60.0,teacher.Notes));
        }
    }

}

public sealed record RosterTargetRow(Guid TeacherId,string Teacher,string EmployeeNumber,decimal Weight,double AvailableHours,double TargetHours,double ActualHours,string Responsibilities);
