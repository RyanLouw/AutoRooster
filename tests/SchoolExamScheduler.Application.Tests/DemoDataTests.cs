using SchoolExamScheduler.Application;
using SchoolExamScheduler.Scheduling;
using Xunit;
public sealed class DemoDataTests
{
    [Fact]
    public void RealisticDemoCoversBroadSubjectCatalogueGradesAndMultipleInvigilators()
    {
        var demo=DemoDataFactory.CreateRealistic(42,new(2026,12,1));var r=demo.Request;
        Assert.Equal(75,r.Teachers.Count);Assert.True(r.Subjects.Count>=35);
        Assert.All(r.Subjects,s=>Assert.Contains(r.Exams,e=>e.SubjectId==s.Id));
        Assert.Equal(new[]{8,9,10,11,12},r.Exams.Select(e=>e.Grade).Distinct().Order());
        Assert.True(r.Exams.Select(e=>e.ExamDate).Distinct().Count()>=15);
        Assert.Contains(r.ExamVenues,v=>v.RequiredInvigilatorCount>1);
        Assert.True(r.Exams.Select(e=>e.Interval.Minutes).Distinct().Count()>=3);
        Assert.Equal(75,r.Teachers.Select(t=>$"{t.FirstName} {t.LastName}").Distinct().Count());
        Assert.True(r.Configuration.AllowSessionSplitting);Assert.Equal(30,r.Configuration.MinimumSessionMinutes);
        Assert.Empty(RequestValidator.Validate(r));Assert.Empty(RosterValidator.Validate(r,demo.KnownFeasibleAssignments));
        var again=DemoDataFactory.CreateRealistic(42,new(2026,12,1));Assert.Equal(r.Exams.Select(e=>e.Id),again.Request.Exams.Select(e=>e.Id));
    }
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
