using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;

public sealed class SolverTests
{
    internal static SchedulingRequest Unsplit(int teachers=2,int positions=1)
    { var r=ValidationTests.Request(teachers,positions);r.Configuration.AllowSessionSplitting=false;return r; }
    private static void Complete(SchedulingRequest r,SchedulingResult result)
    {
        Assert.True(result.Status is SchedulingStatus.Optimal or SchedulingStatus.Feasible,string.Join(";",result.Issues.Select(x=>x.Message)));
        Assert.Empty(RosterValidator.Validate(r,result.Assignments));
    }
    [Theory]
    [InlineData(1,1)]
    [InlineData(8,8)]
    [InlineData(2,2)]
    public async Task IndependentPositionsHaveCompleteCoverage(int teachers,int positions)
    {
        var r=Unsplit(teachers,positions);var result=await new InvigilationSolver().SolveAsync(r);
        Complete(r,result);Assert.Equal(positions,result.Assignments.Count);Assert.Equal(positions*120,result.Assignments.Sum(x=>x.Interval.Minutes));
        Assert.Equal(positions,result.Assignments.Select(x=>x.TeacherId).Distinct().Count());
    }
    [Theory]
    [InlineData(5,8)]
    [InlineData(0,1)]
    public async Task ShortageNeverProducesFalseCompleteRoster(int teachers,int positions)
    {
        var result=await new InvigilationSolver().SolveAsync(Unsplit(teachers,positions));
        Assert.Equal(SchedulingStatus.Infeasible,result.Status);Assert.Empty(result.Assignments);Assert.NotEmpty(result.Issues);
    }
    [Fact]
    public async Task LeaveDailyAndPeriodLimitsAreHardConstraints()
    {
        var r=Unsplit(1);r.Teachers[0].MaximumDailyMinutes=60;
        Assert.Equal(SchedulingStatus.Infeasible,(await new InvigilationSolver().SolveAsync(r)).Status);
        r.Teachers[0].MaximumDailyMinutes=null;r.Teachers[0].MaximumPeriodMinutes=60;
        Assert.Equal(SchedulingStatus.Infeasible,(await new InvigilationSolver().SolveAsync(r)).Status);
        r.Teachers[0].MaximumPeriodMinutes=null;
        r=r with{Availability=[new(r.Teachers[0].Id,r.Exams[0].ExamDate,new(7,0),new(17,0),false)]};
        Assert.Equal(SchedulingStatus.Infeasible,(await new InvigilationSolver().SolveAsync(r)).Status);
    }
    [Fact]
    public async Task WholePeriodReservesOnlyAvailableTeacherForLaterDay()
    {
        var r=Unsplit();var first=r.Exams[0];var exam=new Exam(first.SubjectId,8,first.ExamDate.AddDays(1),new(8,0),new(10,0));var allocation=new ExamVenue(exam.Id,r.Venues[0].Id,30,1);
        r=r with{Exams=[first,exam],ExamVenues=[..r.ExamVenues,allocation],Availability=[new(r.Teachers[0].Id,exam.ExamDate,new(7,0),new(17,0),false)]};
        r.Teachers[1].MaximumPeriodMinutes=120;
        var result=await new InvigilationSolver().SolveAsync(r);Complete(r,result);
        Assert.Equal(r.Teachers[1].Id,result.Assignments.Single(x=>x.ExamVenueId==allocation.Id).TeacherId);
    }
    [Fact]
    public async Task OverlapAndBreakCannotBeViolated()
    {
        var r=Unsplit(1);var first=r.Exams[0];var venue=new Venue("B1",40);
        var exam=new Exam(first.SubjectId,9,first.ExamDate,new(9,0),new(11,0));
        r=r with{Exams=[first,exam],Venues=[..r.Venues,venue],ExamVenues=[..r.ExamVenues,new(exam.Id,venue.Id,30,1)]};
        Assert.Equal(SchedulingStatus.Infeasible,(await new InvigilationSolver().SolveAsync(r)).Status);
        exam=new Exam(first.SubjectId,9,first.ExamDate,new(10,0),new(12,0));
        r=r with{Exams=[first,exam],ExamVenues=[r.ExamVenues[0],new(exam.Id,venue.Id,30,1)]};r.Configuration.MinimumBreakMinutes=30;
        Assert.Equal(SchedulingStatus.Infeasible,(await new InvigilationSolver().SolveAsync(r)).Status);
    }
    [Fact]
    public async Task LocksAreExactAndConflictsRejected()
    {
        var r=Unsplit();var locked=new ScheduledAssignment(r.ExamVenues[0].Id,r.Teachers[1].Id,0,new(8,0),new(10,0));
        r=r with{LockedAssignments=[locked]};var result=await new InvigilationSolver().SolveAsync(r);Complete(r,result);Assert.Contains(locked,result.Assignments);
        r=r with{LockedAssignments=[locked,locked with{TeacherId=r.Teachers[0].Id}]};
        Assert.Equal(SchedulingStatus.InvalidInput,(await new InvigilationSolver().SolveAsync(r)).Status);
    }
    [Fact]
    public async Task EmptyInputAndCancellationHaveAccurateStatuses()
    {
        var r=Unsplit() with{Exams=[],ExamVenues=[]};var result=await new InvigilationSolver().SolveAsync(r);Complete(r,result);Assert.Empty(result.Assignments);
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Assert.Equal(SchedulingStatus.Cancelled,(await new InvigilationSolver().SolveAsync(Unsplit(),cancellation.Token)).Status);
    }
    [Fact]
    public async Task SameSeedAndInputAreDeterministic()
    {
        var r=Unsplit(8,4);var solver=new InvigilationSolver();
        Assert.Equal((await solver.SolveAsync(r)).Assignments,(await solver.SolveAsync(r)).Assignments);
    }
}
