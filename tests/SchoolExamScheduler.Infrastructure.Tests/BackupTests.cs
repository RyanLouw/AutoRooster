using Microsoft.EntityFrameworkCore;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Infrastructure;
using Xunit;
public sealed class BackupTests : IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"backup-tests-"+Guid.NewGuid());
    public void Dispose(){Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(directory))Directory.Delete(directory,true);}
    [Fact]
    public async Task OnlineBackupAndConfirmedRestoreRecoverOriginalDataAndKeepRecoveryCopy()
    {
        var path=Path.Combine(directory,"active.db");var backup=Path.Combine(directory,"manual.db");
        await using(var db=SchedulerDbContext.Open(path)){await db.InitializeAsync();db.Add(new Teacher("Ada","Zulu"));await db.SaveChangesAsync();}
        var maintenance=new DatabaseMaintenance(path,new DatabaseAccess());await maintenance.BackupAsync(backup);
        await using(var db=SchedulerDbContext.Open(path)){db.Add(new Teacher("Bea","Zee"));await db.SaveChangesAsync();}
        await Assert.ThrowsAsync<InvalidOperationException>(()=>maintenance.RestoreAsync(backup,false));
        await maintenance.RestoreAsync(backup,true);
        await using(var db=SchedulerDbContext.Open(path)){Assert.Equal("Ada",(await db.Teachers.SingleAsync()).FirstName);}
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(directory,"backups"),"recovery-*.db"));
    }
    [Fact]
    public async Task CorruptOrUnrelatedBackupCannotChangeActiveDatabase()
    {
        var path=Path.Combine(directory,"active.db");await using(var db=SchedulerDbContext.Open(path)){await db.InitializeAsync();db.Add(new Teacher("Ada","Zulu"));await db.SaveChangesAsync();}
        var bad=Path.Combine(directory,"bad.db");await File.WriteAllTextAsync(bad,"Not a SQLite database");var maintenance=new DatabaseMaintenance(path,new DatabaseAccess());
        await Assert.ThrowsAnyAsync<Exception>(()=>maintenance.RestoreAsync(bad,true));
        await using(var db=SchedulerDbContext.Open(path)){Assert.Single(await db.Teachers.ToListAsync());}
        await Assert.ThrowsAsync<IOException>(()=>maintenance.BackupAsync(bad));Assert.Equal("Not a SQLite database",await File.ReadAllTextAsync(bad));
    }
    [Fact]
    public async Task StoreBatchRollsBackEntireChangeSetOnForeignKeyFailure()
    {
        var path=Path.Combine(directory,"active.db");await using(var db=SchedulerDbContext.Open(path))await db.InitializeAsync();
        var store=new SchoolDataStore(path);var teacher=new Teacher("Ada","Zulu");var invalid=new Exam(Guid.NewGuid(),8,new(2026,12,10),new(8,0),new(10,0));
        await Assert.ThrowsAsync<DbUpdateException>(()=>store.CommitAsync([teacher,invalid],[]));Assert.Empty(await store.ListAsync<Teacher>());
    }
}
