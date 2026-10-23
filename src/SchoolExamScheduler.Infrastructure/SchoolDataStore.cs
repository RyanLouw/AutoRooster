using Microsoft.EntityFrameworkCore;
using SchoolExamScheduler.Application;
namespace SchoolExamScheduler.Infrastructure;
public sealed class SchoolDataStore(string databasePath,DatabaseAccess? sharedAccess=null) : ISchoolDataStore
{
    private readonly DatabaseAccess access=sharedAccess??new();
    public async Task CommitAsync(IReadOnlyList<object> saves,IReadOnlyList<object> deletes,CancellationToken cancellationToken=default)
    {
        using var lease=await access.EnterAsync(cancellationToken);
        await using var db=SchedulerDbContext.Open(databasePath);
        var settings=await db.Settings.AsNoTracking().SingleAsync(cancellationToken);
        if(settings.AutomaticBackupBeforeChanges && (deletes.Count>0 || saves.Any(x=>x is SchoolExamScheduler.Domain.RosterVersion or SchoolExamScheduler.Domain.AuditRecord {Action:"Finish exam series"})))
            await DatabaseMaintenance.BackupCoreAsync(databasePath,Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!,"backups",$"auto-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db"),cancellationToken);
        await using var transaction=await db.Database.BeginTransactionAsync(cancellationToken);
        foreach(var entity in deletes)db.Remove(entity);
        foreach(var entity in saves)
        {
            var entry=db.Entry(entity);var key=entry.Metadata.FindPrimaryKey()??throw new InvalidOperationException("Entity has no key.");
            var values=key.Properties.Select(p=>entry.Property(p.Name).CurrentValue).ToArray();
            var existing=await db.FindAsync(entity.GetType(),values,cancellationToken);
            if(existing is null)db.Add(entity);else db.Entry(existing).CurrentValues.SetValues(entity);
        }
        await db.SaveChangesAsync(cancellationToken);await transaction.CommitAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken=default) where T:class
    {using var lease=await access.EnterAsync(cancellationToken);await using var db=SchedulerDbContext.Open(databasePath);return await db.Set<T>().AsNoTracking().ToListAsync(cancellationToken);}
    public async Task SaveAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class
    {
        using var lease=await access.EnterAsync(cancellationToken);
        await using var db=SchedulerDbContext.Open(databasePath);
        var entry=db.Entry(entity);var key=entry.Metadata.FindPrimaryKey()??throw new InvalidOperationException("Entity has no key.");
        var values=key.Properties.Select(p=>entry.Property(p.Name).CurrentValue).ToArray();
        var existing=await db.Set<T>().FindAsync(values,cancellationToken);
        if(existing is null)db.Add(entity);else db.Entry(existing).CurrentValues.SetValues(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task DeleteAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class
    {using var lease=await access.EnterAsync(cancellationToken);await using var db=SchedulerDbContext.Open(databasePath);db.Remove(entity);await db.SaveChangesAsync(cancellationToken);}
}
