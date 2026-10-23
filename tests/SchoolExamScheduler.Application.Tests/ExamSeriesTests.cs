using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ExamSeriesTests
{
    [Fact]
    public async Task NewGenerationRequestContainsOnlyCurrentActiveExams()
    {
        var store=new ExaminationWorkspaceTests.Store();var subject=new Subject("Maths","M");var venue=new Venue("Hall",30);
        var old=new Exam(subject.Id,8,new(2026,10,1),new(8,0),new(10,0)){IsActive=false};
        var current=new Exam(subject.Id,8,new(2026,11,1),new(8,0),new(9,0));
        store.Items.AddRange([subject,venue,old,current,new ExamVenue(old.Id,venue.Id,30,1),new ExamVenue(current.Id,venue.Id,30,1)]);
        var request=await new RosterWorkflow(store,new InvigilationSolver()).LoadRequestAsync();
        Assert.Equal(current.Id,Assert.Single(request.Exams).Id);Assert.Equal(current.Id,Assert.Single(request.ExamVenues).ExamId);
    }
    private static async Task<(ExaminationWorkspaceTests.Store Store,RosterWorkflow Workflow,ManualRosterService Manual,ReportingService Reports,ExamSeriesService Series,Guid Roster)> School()
    {
        var store=new ExaminationWorkspaceTests.Store();var subject=new Subject("Maths","M");var venue=new Venue("Hall",30);var teacher=new Teacher("First","Teacher");
        var exam=new Exam(subject.Id,8,new(2026,10,1),new(8,0),new(10,0));var allocation=new ExamVenue(exam.Id,venue.Id,30,1);
        store.Items.AddRange([subject,venue,teacher,exam,allocation,new SchedulingSettings{AllowSessionSplitting=false}]);
        var workflow=new RosterWorkflow(store,new InvigilationSolver());await workflow.GenerateAsync("First test series",null);
        var manual=new ManualRosterService(store,workflow);var reports=new ReportingService(store,workflow);
        return(store,workflow,manual,reports,new(store,reports,manual),workflow.LastCreatedRosterId!.Value);
    }
    [Fact]
    public async Task FinishingSeriesKeepsOldReportsStableAndNewStaffStartWithNewHours()
    {
        var (store,workflow,manual,reports,series,id)=await School();var before=await reports.BuildAsync(id);
        await manual.FinalizeAsync(id);var result=await series.FinishAsync();
        Assert.Equal(1,result.ExamsArchived);Assert.Equal(1,result.RostersArchived);
        Assert.True(store.Items.OfType<RosterVersion>().Single().IsArchived);Assert.False(store.Items.OfType<Exam>().Single().IsActive);
        var teacher=store.Items.OfType<Teacher>().Single();teacher.IsActive=false;teacher.FirstName="Changed";
        var newcomer=new Teacher("Next","Teacher");store.Items.Add(newcomer);
        var subject=store.Items.OfType<Subject>().Single();var venue=store.Items.OfType<Venue>().Single();
        var exam=new Exam(subject.Id,9,new(2026,11,1),new(9,0),new(10,0));store.Items.AddRange([exam,new ExamVenue(exam.Id,venue.Id,30,1)]);
        var next=await workflow.GenerateAsync("Second test series",null);
        Assert.True(next.Status is SchedulingStatus.Optimal or SchedulingStatus.Feasible);
        Assert.Equal(60,next.Fairness.TotalMinutes);Assert.All(next.Assignments,a=>Assert.Equal(newcomer.Id,a.TeacherId));
        var historical=await reports.BuildAsync(id);Assert.Equal(before.Period,historical.Period);Assert.Equal(120,historical.RequiredMinutes);Assert.Empty(historical.Unfilled);
        Assert.Equal("First Teacher",Assert.Single(historical.Duties).Teacher);Assert.Equal(before.Fairness.TotalMinutes,historical.Fairness.TotalMinutes);
        Assert.Empty(await manual.ValidateAsync(id));Assert.Throws<InvalidOperationException>(()=>store.Items.OfType<RosterVersion>().Single(r=>r.Id==id).EnsureEditable());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>workflow.GenerateAsync("Change old",id));
        var second=await reports.BuildAsync(workflow.LastCreatedRosterId??store.Items.OfType<RosterVersion>().Single(r=>!r.IsArchived).Id);
        Assert.Equal(60,second.RequiredMinutes);Assert.Single(second.Duties);Assert.Empty(second.Unfilled);
    }
    [Fact]
    public async Task IncompleteArchivedRosterKeepsItsCoverageGapsAndCannotBeEdited()
    {
        var (store,workflow,manual,reports,series,id)=await School();var duty=store.Items.OfType<InvigilationAssignment>().Single();
        await manual.RemoveAsync(id,duty.Id);await series.FinishAsync();
        Assert.Single((await reports.BuildAsync(id)).Unfilled);Assert.NotEmpty(await manual.ValidateAsync(id));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>manual.FinalizeAsync(id));
        var vm=new ReviewRosterViewModel(store,manual,workflow,new Dialogs(true),NullLogger<ReviewRosterViewModel>.Instance){ShowPreviousSeries=true};
        await vm.RefreshAsync();Assert.Empty(vm.Error);Assert.False(vm.CanEdit);Assert.NotEmpty(vm.Issues);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SeriesRolloverRequiresConfirmation(bool confirmed)
    {
        var (store,workflow,manual,reports,series,id)=await School();int notifications=0;
        var vm=new SettingsViewModel(store,null!,null!,new Dialogs(confirmed),NullLogger<SettingsViewModel>.Instance,null,series);
        vm.ExamSeriesStarted+=()=>notifications++;
        await vm.StartNextSeriesCommand.ExecuteAsync(null);
        Assert.Empty(vm.Error);Assert.Equal(confirmed?1:0,notifications);Assert.Equal(confirmed,store.Items.OfType<RosterVersion>().Single().IsArchived);
        Assert.Equal(!confirmed,store.Items.OfType<Exam>().Single().IsActive);
        Assert.Single(store.Items.OfType<Teacher>());Assert.Single(store.Items.OfType<Subject>());Assert.Single(store.Items.OfType<Venue>());
    }
    [Fact]
    public async Task EmptyNextSeriesCannotSaveAnEmptyRoster()
    {
        var (_,workflow,_,_,series,_)=await School();await series.FinishAsync();
        var result=await workflow.GenerateAsync("Empty next series",null);
        Assert.Equal(SchedulingStatus.InvalidInput,result.Status);Assert.Contains(result.Issues,i=>i.Code=="NoExams");
    }
    [Fact]
    public async Task NewSeriesKeepsOnlyTickedTeachersAndHidesPreviousWorkByDefault()
    {
        var (store,workflow,manual,reports,series,id)=await School();var original=store.Items.OfType<Teacher>().Single();
        var retained=new Teacher("Keep","Teacher");store.Items.Add(retained);
        var settings=new SettingsViewModel(store,null!,null!,new Dialogs(true),NullLogger<SettingsViewModel>.Instance,null,series);
        await settings.RefreshAsync();settings.NextSeriesTeachers.Single(t=>t.TeacherId==original.Id).Keep=false;
        await settings.StartNextSeriesCommand.ExecuteAsync(null);Assert.Empty(settings.Error);
        Assert.False(original.IsActive);Assert.True(retained.IsActive);
        var request=await workflow.LoadRequestAsync();Assert.Empty(request.Exams);Assert.Equal(retained.Id,Assert.Single(request.Teachers).Id);
        var teachers=new TeacherWorkspaceViewModel(store,new Dialogs(true),NullLogger<TeacherWorkspaceViewModel>.Instance);await teachers.RefreshAsync();
        Assert.Equal(retained.Id,Assert.Single(teachers.FilteredTeachers).Id);
        var review=new ReviewRosterViewModel(store,manual,workflow,new Dialogs(true),NullLogger<ReviewRosterViewModel>.Instance);await review.RefreshAsync();
        Assert.Empty(review.VisibleRosters);Assert.Empty(review.Duties);review.ShowPreviousSeries=true;
        if(review.LoadDutiesCommand.ExecutionTask is {} load)await load;
        Assert.Single(review.Duties);Assert.False(review.CanEdit);Assert.Empty(review.Error);
    }
    private sealed class Dialogs(bool answer) : IUserDialogs {public Task<bool> ConfirmAsync(string message)=>Task.FromResult(answer);}

}
