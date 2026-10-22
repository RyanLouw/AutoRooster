using SchoolExamScheduler.Application;
using SchoolExamScheduler.Scheduling;
using Xunit;
public sealed class DemoDataTests
{
    [Fact]
    public void SeededSchoolIsRepeatableAndItsKnownFeasibleRosterPassesIndependentValidation()
    {
        var first=DemoDataFactory.Create(42,new(2026,12,1));var second=DemoDataFactory.Create(42,new(2026,12,1));
        Assert.Equal(75,first.Request.Teachers.Count);Assert.Equal(first.Request.Teachers.Select(x=>x.FirstName),second.Request.Teachers.Select(x=>x.FirstName));
        Assert.Empty(RequestValidator.Validate(first.Request));Assert.Empty(RosterValidator.Validate(first.Request,first.KnownFeasibleAssignments));
        Assert.NotEmpty(first.Request.Availability);Assert.Contains(first.Request.Teachers,x=>x.WorkloadWeight!=1);
    }
    [Fact]
    public async Task InfeasibleDemoIsProvedInfeasible()
    {
        var scenario=DemoDataFactory.Create(42,new(2026,12,1),true);
        Assert.Equal(SchedulingStatus.Infeasible,(await new InvigilationSolver().SolveAsync(scenario.Request)).Status);
    }
}
