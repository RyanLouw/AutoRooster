using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;
public sealed class ManualRosterTests
{
    private static async Task<(ExaminationWorkspaceTests.Store Store,ManualRosterService Service,Guid RosterId)> Setup()
    {
        var store=new ExaminationWorkspaceTests.Store();var subject=new Subject("Maths","M");var venue=new Venue("A",30);var exam=new Exam(subject.Id,8,new(2026,12,10),new(8,0),new(10,0));var allocation=new ExamVenue(exam.Id,venue.Id,30,1);
        store.Items.AddRange([subject,venue,exam,allocation,new Teacher("A","One"),new Teacher("B","Two"),new SchedulingSettings(){MinimumBreakMinutes=0,AllowSessionSplitting=false}]);
        var workflow=new RosterWorkflow(store,new InvigilationSolver());await workflow.GenerateAsync("Test",null);
        store.Items.OfType<SchedulingSettings>().Single().AllowSessionSplitting=true;
        return(store,new(store,workflow),workflow.LastCreatedRosterId!.Value);
    }
    [Fact]
    public async Task ReplaceSplitCombineRemoveAndValidateMaintainStatus()
    {
        var (store,service,id)=await Setup();var a=Assert.Single(store.Items.OfType<InvigilationAssignment>());var teacher=store.Items.OfType<Teacher>().Single(t=>t.Id!=a.TeacherId);
        await service.ReplaceAsync(id,a.Id,teacher.Id);a=Assert.Single(store.Items.OfType<InvigilationAssignment>());Assert.Equal(teacher.Id,a.TeacherId);
        await service.SplitAsync(id,a.Id,new(9,0),teacher.Id);var split=store.Items.OfType<InvigilationAssignment>().OrderBy(x=>x.StartTime).ToArray();Assert.Equal(2,split.Length);
        await service.CombineAsync(id,split[0].Id,split[1].Id);a=Assert.Single(store.Items.OfType<InvigilationAssignment>());Assert.Equal(120,a.Interval.Minutes);
        await service.RemoveAsync(id,a.Id);Assert.Equal(RosterStatus.Incomplete,store.Items.OfType<RosterVersion>().Single().Status);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.FinalizeAsync(id));
    }
    [Fact]
    public async Task LockedDutiesAreProtectedAndRegenerationPreservesThem()
    {
        var (store,service,id)=await Setup();var a=Assert.Single(store.Items.OfType<InvigilationAssignment>());
        await service.SetLockAsync(id,a.Id,true);await Assert.ThrowsAsync<InvalidOperationException>(()=>service.RemoveAsync(id,a.Id));
        var workflow=new RosterWorkflow(store,new InvigilationSolver());var result=await workflow.GenerateAsync("Regenerated",id);
        Assert.Contains(result.Assignments,x=>x.TeacherId==a.TeacherId && x.StartTime==a.StartTime && x.EndTime==a.EndTime);
        Assert.Equal(2,store.Items.OfType<RosterVersion>().Count());
        await service.SetLockAsync(id,a.Id,false);await service.FinalizeAsync(id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ReplaceAsync(id,a.Id,a.TeacherId));
        Assert.NotEmpty(store.Items.OfType<AuditRecord>());
    }
    [Fact]
    public async Task InvalidManualChangeCannotBeCommitted()
    {
        var (store,service,id)=await Setup();var a=Assert.Single(store.Items.OfType<InvigilationAssignment>());
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SplitAsync(id,a.Id,new(8,30),a.TeacherId));
        Assert.Single(store.Items.OfType<InvigilationAssignment>());
    }
}
