using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Infrastructure;
using SchoolExamScheduler.Scheduling;
using Xunit;
public sealed class AcceptanceTests
{
    [Fact]
    public async Task RealDatabaseSupportsSchoolGenerationEditingRegenerationExportsRestartAndRestore()
    {
        var directory=Path.Combine(Path.GetTempPath(),"school-acceptance-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try
        {
            var path=Path.Combine(directory,"school.db");await using(var db=SchedulerDbContext.Open(path))await db.InitializeAsync();
            var access=new DatabaseAccess();var store=new SchoolDataStore(path,access);var demo=DemoDataFactory.Create(42,new(2026,12,1));
            demo.Request.Configuration.SolverTimeLimitSeconds=10;await store.CommitAsync(demo.Records,[]);
            var workflow=new RosterWorkflow(store,new InvigilationSolver());var result=await workflow.GenerateAsync("Acceptance",null);
            Assert.True(result.Status is SchedulingStatus.Feasible or SchedulingStatus.Optimal,string.Join(";",result.Issues.Select(x=>x.Message)));
            var rosterId=workflow.LastCreatedRosterId!.Value;var editor=new ManualRosterService(store,workflow);Assert.Empty(await editor.ValidateAsync(rosterId));
            var duty=(await store.ListAsync<InvigilationAssignment>()).First(x=>x.RosterVersionId==rosterId);
            await editor.SetLockAsync(rosterId,duty.Id,true);var regenerated=await workflow.GenerateAsync("Acceptance regenerated",rosterId);
            Assert.Contains(ManualRosterService.ToScheduled(duty),regenerated.Assignments);Assert.Equal(2,(await store.ListAsync<RosterVersion>()).Count);
            var report=await new ReportingService(store,workflow).BuildAsync(rosterId);Assert.Empty(report.Unfilled);Assert.Equal(report.RequiredMinutes,report.AssignedMinutes);
            var exporter=new FileReportExporter();await exporter.ExportExcelAsync(report,Path.Combine(directory,"roster.xlsx"));await exporter.ExportPdfAsync(report,Path.Combine(directory,"teacher.pdf"),ReportKind.TeacherRoster,duty.TeacherId);
            var maintenance=new DatabaseMaintenance(path,access);var backup=Path.Combine(directory,"backup.db");await maintenance.BackupAsync(backup);
            var teacher=(await store.ListAsync<Teacher>()).First();teacher.Notes="Changed after backup";await store.SaveAsync(teacher);await maintenance.RestoreAsync(backup,true);
            var reopened=new SchoolDataStore(path);Assert.Equal(75,(await reopened.ListAsync<Teacher>()).Count);Assert.DoesNotContain(await reopened.ListAsync<Teacher>(),x=>x.Notes=="Changed after backup");Assert.Equal(2,(await reopened.ListAsync<RosterVersion>()).Count);
        }
        finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(directory,true);}
    }
    [Fact]
    public async Task ExistingInitialSchemaMigratesWithoutLosingUserData()
    {
        var directory=Path.Combine(Path.GetTempPath(),"migration-"+Guid.NewGuid());var path=Path.Combine(directory,"school.db");
        try
        {
            await using(var db=SchedulerDbContext.Open(path))
            {
                var initial=db.Database.GetMigrations().First();var migrator=db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();await migrator.MigrateAsync(initial);
                await db.Database.ExecuteSqlRawAsync("INSERT INTO Teachers (Id, FirstName, LastName, IsActive, CanInvigilate, WorkloadWeight, Notes) VALUES ('00000000-0000-0000-0000-000000000001', 'Ada', 'Zulu', 1, 1, '1.0', 'Preserve')");
            }
            await using(var db=SchedulerDbContext.Open(path)){await db.InitializeAsync();Assert.Equal("Preserve",(await db.Teachers.SingleAsync()).Notes);Assert.False((await db.Settings.SingleAsync()).AutomaticBackupBeforeChanges);}
        }
        finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    public async Task SampleSchoolCanGenerateWithThirtyMinuteHandoversEnabled(int seconds)
    {
        var sample=DemoDataFactory.Create(42,new(2026,12,1));var request=sample.Request;
        request.Configuration.AllowSessionSplitting=true;request.Configuration.MinimumSessionMinutes=30;request.Configuration.SchedulingTimeIncrementMinutes=30;
        request.Configuration.SolverTimeLimitSeconds=seconds;
        var result=await new InvigilationSolver().SolveAsync(request);
        Assert.True(result.Status is SchedulingStatus.Feasible or SchedulingStatus.Optimal,string.Join(";",result.Issues.Select(x=>x.Message)));
        Assert.Empty(RosterValidator.Validate(request,result.Assignments));
    }

}
