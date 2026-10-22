namespace SchoolExamScheduler.Infrastructure;
public sealed class DatabaseAccess
{
    private readonly SemaphoreSlim semaphore=new(1,1);
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken=default)
    {await semaphore.WaitAsync(cancellationToken);return new Lease(semaphore);}
    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private bool disposed;
        public void Dispose(){if(disposed)return;disposed=true;semaphore.Release();}
    }
}
