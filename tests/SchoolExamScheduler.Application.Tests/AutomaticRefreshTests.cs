using Microsoft.Extensions.Logging.Abstractions;
using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;

public sealed class AutomaticRefreshTests
{
    private sealed class Files : IFileDialogs
    {
        public Task<string?> SavePathAsync(string title,string filter,string extension)=>Task.FromResult<string?>(null);
        public Task<string?> OpenPathAsync(string title,string filter)=>Task.FromResult<string?>(null);
    }
    private sealed class Exporter : IReportExporter
    {
        public Task ExportAllAsync(ReportSnapshot report,string path,CancellationToken cancellationToken=default)=>Task.CompletedTask;
        public Task ExportExcelAsync(ReportSnapshot report,string path,CancellationToken cancellationToken=default)=>Task.CompletedTask;
        public Task ExportPdfAsync(ReportSnapshot report,string path,ReportKind kind,Guid? teacherId=null,CancellationToken cancellationToken=default)=>Task.CompletedTask;
    }
    [Fact]
    public async Task ReportsLoadAutomaticallyPreserveSelectedVersionAndClearWhenNoVersionsRemain()
    {
        var store=new ExaminationWorkspaceTests.Store();var first=new RosterVersion("First",42,"test","{}");
        store.Items.AddRange([first,new SchedulingSettings()]);
        var vm=new ReportsViewModel(store,new ReportingService(store,new RosterWorkflow(store,new InvigilationSolver())),new Exporter(),new Files(),NullLogger<ReportsViewModel>.Instance);
        await vm.RefreshAsync();
        Assert.Equal(first.Id,vm.SelectedRoster?.Id);Assert.Equal("First",vm.Report?.RosterName);
        var second=new RosterVersion("Second",42,"test","{}");store.Items.Add(second);
        var teacher=new Teacher("Ada","Zulu");store.Items.Add(teacher);
        await vm.RefreshAsync();Assert.Equal(first.Id,vm.SelectedRoster?.Id);Assert.Contains(vm.Teachers,t=>t.Id==teacher.Id);
        vm.SelectedRoster=second;
        if(vm.BuildReportCommand.ExecutionTask is {} refresh)await refresh;
        Assert.Equal("Second",vm.Report?.RosterName);
        store.Items.RemoveAll(x=>x is RosterVersion);await vm.RefreshAsync();
        Assert.Null(vm.Report);Assert.Empty(vm.Workloads);Assert.Empty(vm.Error);
    }
    [Fact]
    public async Task ReviewLoadsDutiesAndValidationAutomatically()
    {
        var store=new ExaminationWorkspaceTests.Store();var roster=new RosterVersion("Review",42,"test","{}");
        var subject=new Subject("Maths","M");var venue=new Venue("Hall",30);var teacher=new Teacher("Ada","Zulu");
        var exam=new Exam(subject.Id,8,new(2026,12,1),new(8,0),new(9,0));var allocation=new ExamVenue(exam.Id,venue.Id,30,1);
        store.Items.AddRange([roster,subject,venue,teacher,exam,allocation,new SchedulingSettings(),new InvigilationAssignment(roster.Id,allocation.Id,teacher.Id,0,new(8,0),new(9,0))]);
        var workflow=new RosterWorkflow(store,new InvigilationSolver());
        var vm=new ReviewRosterViewModel(store,new ManualRosterService(store,workflow),workflow,new Dialogs(),NullLogger<ReviewRosterViewModel>.Instance);
        await vm.RefreshAsync();Assert.Single(vm.Duties);Assert.Empty(vm.Issues);Assert.Empty(vm.Error);
        var second=new RosterVersion("Empty",42,"test","{}");store.Items.Add(second);
        await vm.RefreshAsync();vm.SelectedRoster=second;
        if(vm.LoadDutiesCommand.ExecutionTask is {} refresh)await refresh;
        Assert.Empty(vm.Duties);Assert.NotEmpty(vm.Issues);Assert.Empty(vm.Error);
        store.Items.RemoveAll(x=>x is RosterVersion);await vm.RefreshAsync();Assert.Empty(vm.Duties);Assert.Empty(vm.Issues);
    }
    private sealed class Dialogs : IUserDialogs {public Task<bool> ConfirmAsync(string message)=>Task.FromResult(true);}
}
