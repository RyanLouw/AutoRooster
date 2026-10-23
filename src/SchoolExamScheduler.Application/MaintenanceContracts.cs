namespace SchoolExamScheduler.Application;
public interface IDatabaseMaintenance
{
    Task BackupAsync(string destination,CancellationToken cancellationToken=default);
    Task RestoreAsync(string source,bool confirmed,CancellationToken cancellationToken=default);
}
public interface IDemoLauncher { Task OpenAsync(); }
