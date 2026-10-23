using SchoolExamScheduler.Application;
using SchoolExamScheduler.Infrastructure;
using SchoolExamScheduler.Licensing;
using SchoolExamScheduler.Scheduling;
using Xunit;
namespace SchoolExamScheduler.Licensing.Tests;
public sealed class EnforcementTests
{
    [Fact] public async Task Denied_licence_blocks_all_persistence_and_generation_but_allows_reads()
    {
        var inner=new MemoryStore();var gate=new DeniedGate();var store=new LicensedSchoolDataStore(inner,gate);
        Assert.Empty(await store.ListAsync<object>());
        await Assert.ThrowsAsync<LicenceRequiredException>(()=>store.SaveAsync(new object()));
        await Assert.ThrowsAsync<LicenceRequiredException>(()=>store.DeleteAsync(new object()));
        await Assert.ThrowsAsync<LicenceRequiredException>(()=>store.CommitAsync([],[]));
        await Assert.ThrowsAsync<LicenceRequiredException>(()=>new RosterWorkflow(store,new InvigilationSolver(),gate).GenerateAsync("Blocked",null));
        Assert.Equal(0,inner.Writes);Assert.Equal(1,inner.Reads);
    }
    private sealed class DeniedGate:ILicenceGate {public Task RequireWriteAccessAsync(CancellationToken cancellationToken=default)=>throw new LicenceRequiredException("Disabled");}
    private sealed class MemoryStore:ISchoolDataStore
    {
        public int Reads,Writes;
        public Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken=default) where T:class {Reads++;return Task.FromResult<IReadOnlyList<T>>([]);}
        public Task SaveAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class {Writes++;return Task.CompletedTask;}
        public Task DeleteAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class {Writes++;return Task.CompletedTask;}
        public Task CommitAsync(IReadOnlyList<object> saves,IReadOnlyList<object> deletes,CancellationToken cancellationToken=default){Writes++;return Task.CompletedTask;}
    }
}
