using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;

public sealed class SplittingTests
{
    private static SchedulingRequest SplitRequest(int positions=1)
    {
        var r=ValidationTests.Request(positions*2,positions);var date=r.Exams[0].ExamDate;
        return r with{Availability=r.Teachers.Select((t,i)=>new TeacherAvailability(t.Id,date,new(i%2==0?8:9,0),new(i%2==0?9:10,0),true)).ToArray()};
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public async Task SplitSessionsCoverEachIndependentPositionWithoutGaps(int positions)
    {
        var r=SplitRequest(positions);var result=await new InvigilationSolver().SolveAsync(r);
        Assert.True(result.Status is SchedulingStatus.Optimal or SchedulingStatus.Feasible);
        Assert.Empty(RosterValidator.Validate(r,result.Assignments));Assert.Equal(positions*2,result.Assignments.Count);
    }
    [Fact]
    public async Task SegmentLimitsAndMinimumLengthAreHardConstraints()
    {
        var r=SplitRequest();r.Configuration.MaximumSessionSegmentsPerPosition=1;
        Assert.Equal(SchedulingStatus.Infeasible,(await new InvigilationSolver().SolveAsync(r)).Status);
        r.Configuration.MaximumSessionSegmentsPerPosition=3;r.Configuration.MinimumSessionMinutes=75;
        Assert.Equal(SchedulingStatus.Infeasible,(await new InvigilationSolver().SolveAsync(r)).Status);
    }
    [Fact]
    public async Task OffGridExamAndLockedBoundariesArePreservedExactly()
    {
        var r=ValidationTests.Request(20);var exam=new Exam(r.Subjects[0].Id,8,r.Exams[0].ExamDate,new(8,7),new(10,13));
        var allocation=new ExamVenue(exam.Id,r.Venues[0].Id,30,1);
        var locked=new ScheduledAssignment(allocation.Id,r.Teachers[0].Id,0,new(8,7),new(9,10));
        r=r with{Exams=[exam],ExamVenues=[allocation],LockedAssignments=[locked]};
        var result=await new InvigilationSolver().SolveAsync(r);
        Assert.True(result.Status is SchedulingStatus.Optimal or SchedulingStatus.Feasible);
        Assert.Contains(locked,result.Assignments);Assert.Empty(RosterValidator.Validate(r,result.Assignments));
        Assert.Equal(126,result.Assignments.Sum(x=>x.Interval.Minutes));
    }
}
