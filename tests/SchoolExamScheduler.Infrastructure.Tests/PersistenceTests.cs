using Microsoft.EntityFrameworkCore;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Infrastructure;
using Xunit;

public sealed class PersistenceTests : IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"school-tests-"+Guid.NewGuid());
    private string DatabasePath => Path.Combine(directory,"school.db");
    private SchedulerDbContext Open() => SchedulerDbContext.Open(DatabasePath);
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if(Directory.Exists(directory)) Directory.Delete(directory,true); }

    [Fact]
    public async Task MigrationsPersistAllEntitiesAndDatesAcrossRestart()
    {
        Guid teacherId,examId,rosterId;
        await using(var db=Open())
        {
            await db.InitializeAsync();
            Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
            var teacher=new Teacher("Ada","Zulu",0.5m);teacherId=teacher.Id;
            var subject=new Subject("Maths","M");var venue=new Venue("A1",40);
            var exam=new Exam(subject.Id,8,new(2026,12,10),new(8,15),new(10,30));examId=exam.Id;
            var allocation=new ExamVenue(exam.Id,venue.Id,35,2);
            var roster=new RosterVersion("December",42,"test","{}");rosterId=roster.Id;
            db.AddRange(teacher,subject,venue,exam,allocation,roster);
            db.Add(new TeacherAvailability(teacher.Id,exam.ExamDate,new(11,0),new(12,0),false));
            db.Add(new VenueUnavailability(venue.Id,exam.ExamDate,new(12,0),new(13,0)));
            db.Add(new InvigilationAssignment(roster.Id,allocation.Id,teacher.Id,0,new(8,15),new(10,30)));
            db.Add(new AuditRecord("Create","Created initial roster"));
            await db.SaveChangesAsync();
        }
        await using(var db=Open())
        {
            await db.InitializeAsync();
            var teacher=await db.Teachers.SingleAsync(x=>x.Id==teacherId);
            Assert.Equal(0.5m,teacher.WorkloadWeight);
            var exam=await db.Exams.SingleAsync(x=>x.Id==examId);
            Assert.Equal(new DateOnly(2026,12,10),exam.ExamDate);
            Assert.Equal(new TimeOnly(8,15),exam.StartTime);
            Assert.Equal(135,(await db.Assignments.SingleAsync()).Interval.Minutes);
            Assert.Equal(rosterId,(await db.Rosters.SingleAsync()).Id);
            Assert.Single(await db.Availability.ToListAsync());
            Assert.Single(await db.VenueUnavailability.ToListAsync());
            Assert.Single(await db.Audit.ToListAsync());
            var settings=await db.Settings.SingleAsync();settings.SchoolName="Example School";
            teacher.LastName="Updated";await db.SaveChangesAsync();
        }
        await using(var db=Open())
        {
            Assert.Equal("Updated",(await db.Teachers.SingleAsync()).LastName);
            Assert.Equal("Example School",(await db.Settings.SingleAsync()).SchoolName);
        }
    }
    [Fact]
    public async Task TransactionsRollbackAndForeignKeysRestrictDeletion()
    {
        await using var db=Open();await db.InitializeAsync();
        var subject=new Subject("English","E");db.Add(subject);await db.SaveChangesAsync();
        await using(var transaction=await db.Database.BeginTransactionAsync())
        {
            db.Add(new Venue("Transient",20));await db.SaveChangesAsync();await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();Assert.Empty(await db.Venues.ToListAsync());
        db.Add(new Exam(subject.Id,9,new(2026,12,10),new(8,0),new(9,0)));await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); // Exercise SQLite restriction rather than EF tracked relationship validation.
        db.Remove(await db.Subjects.SingleAsync());await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
    }
    [Fact]
    public async Task UniqueConstraintsAndForeignKeysAreEnforced()
    {
        await using var db=Open();await db.InitializeAsync();
        db.Add(new Subject("Maths","M"));await db.SaveChangesAsync();
        db.Add(new Subject("Duplicate","M"));await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        db.ChangeTracker.Clear();db.Add(new Exam(Guid.NewGuid(),8,new(2026,12,10),new(8,0),new(9,0)));
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
    }
}
