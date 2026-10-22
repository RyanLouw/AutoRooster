using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SchoolExamScheduler.Application;
namespace SchoolExamScheduler.Infrastructure;
public sealed class DatabaseMaintenance(string databasePath,DatabaseAccess access) : IDatabaseMaintenance
{
    public async Task BackupAsync(string destination,CancellationToken cancellationToken=default)
    {using var lease=await access.EnterAsync(cancellationToken);await BackupCoreAsync(databasePath,destination,cancellationToken);}
    internal static async Task BackupCoreAsync(string source,string destination,CancellationToken cancellationToken)
    {
        if(File.Exists(destination))throw new IOException("Backup destination already exists. Choose a new filename.");
        var directory=Path.GetDirectoryName(Path.GetFullPath(destination))!;Directory.CreateDirectory(directory);
        var temporary=Path.Combine(directory,"."+Guid.NewGuid()+".db");
        try
        {
            await using(var input=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ConnectionString))
            await using(var output=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=temporary,Pooling=false}.ConnectionString))
            {await input.OpenAsync(cancellationToken);await output.OpenAsync(cancellationToken);await Task.Run(()=>input.BackupDatabase(output),cancellationToken);}
            await ValidateAsync(temporary,cancellationToken);cancellationToken.ThrowIfCancellationRequested();File.Move(temporary,destination,false);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    private static async Task ValidateAsync(string path,CancellationToken cancellationToken)
    {
        if(!File.Exists(path))throw new FileNotFoundException("Backup file was not found.",path);
        await using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false,ForeignKeys=true}.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command=connection.CreateCommand();command.CommandText="PRAGMA integrity_check";
        if(!string.Equals(await command.ExecuteScalarAsync(cancellationToken) as string,"ok",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Backup integrity check failed.");
        command.CommandText="PRAGMA foreign_key_check";
        await using(var reader=await command.ExecuteReaderAsync(cancellationToken))if(await reader.ReadAsync(cancellationToken))throw new InvalidDataException("Backup contains broken foreign keys.");
        var options=new DbContextOptionsBuilder<SchedulerDbContext>().UseSqlite(connection).Options;
        await using var db=new SchedulerDbContext(options);
        var applied=(await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Order().ToArray();var known=db.Database.GetMigrations().Order().ToArray();
        if(!applied.SequenceEqual(known))throw new InvalidDataException("Backup schema differs from this application version. Use a compatible application version to migrate it first.");
        if(await db.Settings.CountAsync(cancellationToken)!=1)throw new InvalidDataException("Backup is not a valid school scheduler database.");
        (await db.Settings.SingleAsync(cancellationToken)).Validate();
    }
    public async Task RestoreAsync(string source,bool confirmed,CancellationToken cancellationToken=default)
    {
        if(!confirmed)throw new InvalidOperationException("Explicit confirmation is required before replacing the active database.");
        if(Path.GetFullPath(source)==Path.GetFullPath(databasePath))throw new ArgumentException("Choose a separate backup file.");
        using var lease=await access.EnterAsync(cancellationToken);
        await ValidateAsync(source,cancellationToken);
        var recovery=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!,"backups",$"recovery-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db");
        await BackupCoreAsync(databasePath,recovery,cancellationToken);
        await using var input=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ConnectionString);
        await using var output=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=databasePath,ForeignKeys=true,Pooling=false}.ConnectionString);
        await input.OpenAsync(cancellationToken);await output.OpenAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // sqlite3_backup commits atomically on completion and rolls back a failed backup.
        // Never copy an active SQLite database file or its WAL by hand.
        await Task.Run(()=>input.BackupDatabase(output),CancellationToken.None);
        SqliteConnection.ClearAllPools();
    }
}
