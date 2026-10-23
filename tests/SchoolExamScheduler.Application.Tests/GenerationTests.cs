using Microsoft.Extensions.Logging.Abstractions;
using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;
public sealed class GenerationTests
{
    [Fact]
    public async Task TargetPreviewRecalculatesAfterResponsibilityAndAvailabilityChanges()
    {
        var demo=DemoDataFactory.Create(42,new(2026,12,1));var store=new ExaminationWorkspaceTests.Store();store.Items.AddRange(demo.Records);
        var vm=new GenerationWorkspaceViewModel(new RosterWorkflow(store,new InvigilationSolver()),NullLogger<GenerationWorkspaceViewModel>.Instance);
        await vm.RefreshAsync();Assert.Equal(75,vm.Targets.Count);Assert.Equal(200.0/75,vm.AverageHours,5);
        var teacher=demo.Request.Teachers[41];var previous=vm.Targets.Single(t=>t.TeacherId==teacher.Id).TargetHours;
        teacher.WorkloadWeight=0.5m;await vm.RefreshAsync();Assert.True(vm.Targets.Single(t=>t.TeacherId==teacher.Id).TargetHours<previous);
        foreach(var date in demo.Request.Exams.Select(e=>e.ExamDate).Distinct())store.Items.Add(new TeacherAvailability(teacher.Id,date,new(7,0),new(17,0),false));
        await vm.RefreshAsync();Assert.Equal(0,vm.Targets.Single(t=>t.TeacherId==teacher.Id).TargetHours);
        Assert.Equal(200,vm.Targets.Sum(t=>t.TargetHours),5);Assert.Empty(vm.Error);
    }
    [Fact]
    public async Task GenerateCommandPersistsValidatedVersionAndDuties()
    {
        var store=new ExaminationWorkspaceTests.Store();var teacher=new Teacher("A","B");var subject=new Subject("Maths","M");var venue=new Venue("A1",30);var exam=new Exam(subject.Id,8,new(2026,12,10),new(8,0),new(10,0));var allocation=new ExamVenue(exam.Id,venue.Id,30,1);
        store.Items.AddRange([teacher,subject,venue,exam,allocation,new SchedulingSettings()]);
        var workflow=new RosterWorkflow(store,new InvigilationSolver());
        var vm=new GenerationWorkspaceViewModel(workflow,NullLogger<GenerationWorkspaceViewModel>.Instance);
        await vm.ValidateInputsCommand.ExecuteAsync(null);Assert.Empty(vm.Error);Assert.Equal(120,vm.RequiredMinutes);
        await vm.GenerateCommand.ExecuteAsync(null);Assert.Empty(vm.Error);
        Assert.Equal(RosterStatus.Validated,Assert.Single(store.Items.OfType<RosterVersion>()).Status);
        Assert.Equal(120,Assert.Single(store.Items.OfType<InvigilationAssignment>()).Interval.Minutes);
        Assert.False(vm.IsBusy);Assert.NotNull(vm.Result);
    }
    [Fact]
    public async Task InvalidInputDoesNotPersistFalseValidatedRoster()
    {
        var store=new ExaminationWorkspaceTests.Store();var subject=new Subject("Maths","M");store.Items.Add(subject);store.Items.Add(new Exam(subject.Id,8,new(2026,12,10),new(8,0),new(10,0)));
        var vm=new GenerationWorkspaceViewModel(new RosterWorkflow(store,new InvigilationSolver()),NullLogger<GenerationWorkspaceViewModel>.Instance);
        await vm.GenerateCommand.ExecuteAsync(null);Assert.NotEmpty(vm.Error);Assert.Empty(store.Items.OfType<RosterVersion>());
    }
    [Fact]
    public async Task CancellationLeavesDatabaseUnchanged()
    {
        var store=new ExaminationWorkspaceTests.Store();var workflow=new RosterWorkflow(store,new InvigilationSolver());using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        var result=await workflow.GenerateAsync("Test",null,cancellation.Token);
        Assert.Equal(SchedulingStatus.Cancelled,result.Status);Assert.Empty(store.Items.OfType<RosterVersion>());
    }
}
