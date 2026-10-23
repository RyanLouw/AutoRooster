using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Infrastructure;
using SchoolExamScheduler.Scheduling;
using Xunit;

public sealed class ExamSeriesPersistenceTests : IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"exam-series-"+Guid.NewGuid());
    public void Dispose(){Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(directory))Directory.Delete(directory,true);}
    [Fact]
    public async Task RolloverSurvivesRestartKeepsStaffSelectionAndFrozenReportsAndCreatesConfiguredBackup()
    {
        var path=Path.Combine(directory,"school.db");await using(var db=SchedulerDbContext.Open(path))await db.InitializeAsync();
        var store=new SchoolDataStore(path);var subject=new Subject("Maths","M");var venue=new Venue("Hall",30);
        var leaving=new Teacher("Leaving","Teacher");var staying=new Teacher("Staying","Teacher");
        var exam=new Exam(subject.Id,8,new(2026,10,1),new(8,0),new(10,0));var allocation=new ExamVenue(exam.Id,venue.Id,30,1);
        await store.CommitAsync([subject,venue,leaving,staying,exam,allocation,new SchedulingSettings{AllowSessionSplitting=false,AutomaticBackupBeforeChanges=true}],[]);
        var workflow=new RosterWorkflow(store,new InvigilationSolver());await workflow.GenerateAsync("First series",null);var oldId=workflow.LastCreatedRosterId!.Value;
        var reports=new ReportingService(store,workflow);var before=await reports.BuildAsync(oldId);
        var backupCount=Directory.GetFiles(Path.Combine(directory,"backups"),"auto-*.db").Length;
        await new ExamSeriesService(store,reports,new ManualRosterService(store,workflow)).FinishAsync([staying.Id]);
        Assert.True(Directory.GetFiles(Path.Combine(directory,"backups"),"auto-*.db").Length>backupCount);
        store=new SchoolDataStore(path);workflow=new RosterWorkflow(store,new InvigilationSolver());reports=new ReportingService(store,workflow);
        Assert.True((await store.ListAsync<RosterVersion>()).Single().IsArchived);Assert.Empty((await workflow.LoadRequestAsync()).Exams);
        Assert.Equal(staying.Id,Assert.Single((await workflow.LoadRequestAsync()).Teachers).Id);
        var departed=(await store.ListAsync<Teacher>()).Single(t=>t.Id==leaving.Id);Assert.False(departed.IsActive);departed.FirstName="Changed later";await store.SaveAsync(departed);
        var newTeacher=new Teacher("New","Teacher");var next=new Exam(subject.Id,9,new(2026,11,1),new(8,0),new(9,0));
        await store.CommitAsync([newTeacher,next,new ExamVenue(next.Id,venue.Id,30,1)],[]);
        var result=await workflow.GenerateAsync("Second series",null);Assert.True(result.Status is SchedulingStatus.Optimal or SchedulingStatus.Feasible);
        Assert.Equal(60,result.Fairness.TotalMinutes);Assert.DoesNotContain(result.Assignments,a=>a.TeacherId==leaving.Id);
        var current=await reports.BuildAsync(workflow.LastCreatedRosterId!.Value);Assert.Equal(60,current.RequiredMinutes);Assert.DoesNotContain(leaving.Id,current.TeacherNames.Keys);
        var historical=await reports.BuildAsync(oldId);Assert.Equal(before.RequiredMinutes,historical.RequiredMinutes);Assert.Equal(before.Period,historical.Period);
        Assert.Equal("Leaving Teacher",historical.TeacherNames[leaving.Id]);Assert.Empty(historical.Unfilled);
        Assert.Equal(before.Fairness.TotalMinutes,historical.Fairness.TotalMinutes);
        await new FileReportExporter().ExportExcelAsync(historical,Path.Combine(directory,"Previous-series.xlsx"));
    }
    [Fact]
    public async Task ExistingRosterBeforeArchiveMigrationRemainsUnarchivedAndReadable()
    {
        var path=Path.Combine(directory,"upgrade.db");var id=Guid.NewGuid();
        await using(var db=SchedulerDbContext.Open(path))
        {
            var previous=db.Database.GetMigrations().Single(m=>m.EndsWith("AutomaticBackupSetting",StringComparison.Ordinal));
            await db.GetService<IMigrator>().MigrateAsync(previous);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Rosters (Id, Name, CreatedOn, Status, SchedulingSeed, AlgorithmVersion, SettingsSnapshot) VALUES ({id}, {"Legacy"}, {DateTimeOffset.UtcNow.UtcTicks}, {2}, {42}, {"test"}, {"{}"})");
        }
        await using(var db=SchedulerDbContext.Open(path))
        {
            await db.InitializeAsync();var roster=await db.Rosters.SingleAsync();
            Assert.Equal(id,roster.Id);Assert.Equal("Legacy",roster.Name);Assert.False(roster.IsArchived);Assert.Equal("",roster.ArchivedSnapshot);Assert.Equal(RosterStatus.Validated,roster.Status);
        }
    }
}
