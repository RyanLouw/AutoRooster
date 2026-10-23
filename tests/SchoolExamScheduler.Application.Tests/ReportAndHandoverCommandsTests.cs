using Microsoft.Extensions.Logging.Abstractions;
using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;

public sealed class ReportAndHandoverCommandsTests
{
    private sealed class Files(string? path) : IFileDialogs
    {
        public Task<string?> SavePathAsync(string title,string filter,string extension)=>Task.FromResult(path);
        public Task<string?> OpenPathAsync(string title,string filter)=>Task.FromResult<string?>(null);
    }
    private sealed class Dialogs(bool answer) : IUserDialogs
    {public Task<bool> ConfirmAsync(string message)=>Task.FromResult(answer);}
    private sealed class Maintenance : IDatabaseMaintenance
    {
        public Task BackupAsync(string path,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task RestoreAsync(string path,bool confirmed,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
    }
    private sealed class Exporter : IReportExporter
    {
        public int Bundles {get;private set;}
        public ReportKind? LastPdf {get;private set;}
        public Task ExportAllAsync(ReportSnapshot report,string path,CancellationToken cancellationToken=default){Bundles++;return Task.CompletedTask;}
        public Task ExportExcelAsync(ReportSnapshot report,string path,CancellationToken cancellationToken=default)=>Task.CompletedTask;
        public Task ExportPdfAsync(ReportSnapshot report,string path,ReportKind kind,Guid? teacherId=null,CancellationToken cancellationToken=default){LastPdf=kind;return Task.CompletedTask;}
    }
    private sealed class DemoLauncher : IDemoLauncher
    {
        public int OpenCount {get;private set;}
        public Task OpenAsync(){OpenCount++;return Task.CompletedTask;}
    }
    [Fact]
    public async Task RealisticDemoLauncherDoesNotModifyTheCurrentSchool()
    {
        var store=new ExaminationWorkspaceTests.Store();var teacher=new Teacher("Real","Teacher");store.Items.Add(teacher);
        var launcher=new DemoLauncher();var vm=new SettingsViewModel(store,new Maintenance(),new Files(null),new Dialogs(true),NullLogger<SettingsViewModel>.Instance,launcher);
        await vm.OpenRealisticDemoCommand.ExecuteAsync(null);
        Assert.Empty(vm.Error);Assert.Equal(1,launcher.OpenCount);Assert.Same(teacher,Assert.Single(store.Items));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ThirtyMinutePresetRequiresConfirmationAndPreservesOtherSettings(bool confirmed)
    {
        var store=new ExaminationWorkspaceTests.Store();var settings=new SchedulingSettings{SchoolName="My school",AllowSessionSplitting=false,MinimumSessionMinutes=60,MaximumSessionSegmentsPerPosition=1};store.Items.Add(settings);
        var vm=new SettingsViewModel(store,new Maintenance(),new Files(null),new Dialogs(confirmed),NullLogger<SettingsViewModel>.Instance){Settings=settings};
        await vm.EnableThirtyMinuteHandoversCommand.ExecuteAsync(null);Assert.Empty(vm.Error);
        Assert.Equal(confirmed,settings.AllowSessionSplitting);Assert.Equal(confirmed?30:60,settings.MinimumSessionMinutes);
        Assert.Equal("My school",settings.SchoolName);if(confirmed)Assert.True(settings.MaximumSessionSegmentsPerPosition>=2);
    }
    [Fact]
    public async Task ExportAllAndTeacherHoursCommandsAreAvailableWithoutSelectingIndividualTeacher()
    {
        var store=new ExaminationWorkspaceTests.Store();var roster=new RosterVersion("Test",42,"test","{}");store.Items.AddRange([roster,new SchedulingSettings(),new Teacher("Ada","Zulu")]);
        var service=new ReportingService(store,new RosterWorkflow(store,new InvigilationSolver()));var exporter=new Exporter();
        var vm=new ReportsViewModel(store,service,exporter,new Files("report.zip"),NullLogger<ReportsViewModel>.Instance){SelectedRoster=roster};
        await vm.ExportAllCommand.ExecuteAsync(null);Assert.Empty(vm.Error);Assert.Equal(1,exporter.Bundles);
        await vm.ExportTeacherHoursCommand.ExecuteAsync(null);Assert.Empty(vm.Error);Assert.Equal(ReportKind.Workload,exporter.LastPdf);
    }
    [Fact]
    public async Task CancellingSaveDialogDoesNotCreateReportBundle()
    {
        var store=new ExaminationWorkspaceTests.Store();var roster=new RosterVersion("Test",42,"test","{}");store.Items.Add(roster);var exporter=new Exporter();
        var vm=new ReportsViewModel(store,new ReportingService(store,new RosterWorkflow(store,new InvigilationSolver())),exporter,new Files(null),NullLogger<ReportsViewModel>.Instance){SelectedRoster=roster};
        await vm.ExportAllCommand.ExecuteAsync(null);Assert.Empty(vm.Error);Assert.Equal(0,exporter.Bundles);
    }
}
