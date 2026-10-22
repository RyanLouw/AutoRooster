using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;
public sealed class ReportingTests
{
    [Fact]
    public async Task ReportsShowAccurateMinutesTeacherTargetsAndCoverageGaps()
    {
        var store=new ExaminationWorkspaceTests.Store();var subject=new Subject("Maths","M");var venue=new Venue("A",30);var exam=new Exam(subject.Id,8,new(2026,12,10),new(8,0),new(10,0));var allocation=new ExamVenue(exam.Id,venue.Id,30,2);var teacher=new Teacher("Ada","Zulu");var second=new Teacher("Bea","Zee");var roster=new RosterVersion("December",42,"test","{}");
        store.Items.AddRange([subject,venue,exam,allocation,teacher,second,roster,new SchedulingSettings(),new InvigilationAssignment(roster.Id,allocation.Id,teacher.Id,0,new(8,0),new(10,0))]);
        var report=await new ReportingService(store,new RosterWorkflow(store,new InvigilationSolver())).BuildAsync(roster.Id);
        Assert.Equal(240,report.RequiredMinutes);Assert.Equal(120,report.AssignedMinutes);Assert.Single(report.Unfilled);
        Assert.Equal(120,report.Unfilled[0].Minutes);Assert.Equal("Ada Zulu",Assert.Single(report.Duties).Teacher);
        Assert.Equal(2,report.TotalTeachers);Assert.Equal(1,report.Fairness.UnusedEligibleTeachers);
        Assert.Equal(120,report.Fairness.Teachers.Single(x=>x.TeacherId==second.Id).TargetMinutes,5);
        Assert.Equal("School",report.SchoolName);
        teacher.IsActive=false;
        report=await new ReportingService(store,new RosterWorkflow(store,new InvigilationSolver())).BuildAsync(roster.Id);
        Assert.Equal(report.AssignedMinutes,report.Fairness.TotalMinutes);
        Assert.Equal(0,report.Fairness.Teachers.Single(x=>x.TeacherId==teacher.Id).TargetMinutes);
    }
}
