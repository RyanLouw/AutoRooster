using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
namespace SchoolExamScheduler.Application;

public interface ISchoolDataStore
{
    Task CommitAsync(IReadOnlyList<object> saves,IReadOnlyList<object> deletes,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken=default) where T:class;
    Task SaveAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class;
    Task DeleteAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class;
}
public interface IUserDialogs { Task<bool> ConfirmAsync(string message); }
public abstract partial class WorkspaceViewModel(ILogger logger) : ObservableObject
{
    public virtual Task RefreshAsync()=>Task.CompletedTask;
    [ObservableProperty] private string error="";
    [ObservableProperty] private bool isBusy;
    private int activeOperations;
    protected async Task RunAsync(Func<Task> action)
    {
        Interlocked.Increment(ref activeOperations);IsBusy=true;Error="";
        try{await action();}
        catch(Exception e) {logger.LogError(e,"Workspace operation failed");Error=e.Message;}
        finally {IsBusy=Interlocked.Decrement(ref activeOperations)>0;}
    }
}
