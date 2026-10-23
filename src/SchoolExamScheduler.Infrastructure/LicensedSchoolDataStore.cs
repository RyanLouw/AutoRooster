using SchoolExamScheduler.Application;
using SchoolExamScheduler.Licensing;
namespace SchoolExamScheduler.Infrastructure;

/// <summary>Reads and exports remain available; every persisted change needs a current licence.</summary>
public sealed class LicensedSchoolDataStore(ISchoolDataStore inner,ILicenceGate licence):ISchoolDataStore
{
    public Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken=default) where T:class=>inner.ListAsync<T>(cancellationToken);
    public async Task SaveAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class
    {await licence.RequireWriteAccessAsync(cancellationToken);await inner.SaveAsync(entity,cancellationToken);}
    public async Task DeleteAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class
    {await licence.RequireWriteAccessAsync(cancellationToken);await inner.DeleteAsync(entity,cancellationToken);}
    public async Task CommitAsync(IReadOnlyList<object> saves,IReadOnlyList<object> deletes,CancellationToken cancellationToken=default)
    {await licence.RequireWriteAccessAsync(cancellationToken);await inner.CommitAsync(saves,deletes,cancellationToken);}
}
