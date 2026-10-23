using Microsoft.Extensions.Logging.Abstractions;
using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using Xunit;
public sealed class ExaminationWorkspaceTests
{
    internal sealed class Store : ISchoolDataStore
    {
        public List<object> Items {get;}=[];
        public Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken=default) where T:class =>Task.FromResult<IReadOnlyList<T>>(Items.OfType<T>().ToArray());
        public Task SaveAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class {if(!Items.Contains(entity))Items.Add(entity);return Task.CompletedTask;}
        public Task DeleteAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class {Items.Remove(entity);return Task.CompletedTask;}
        public Task CommitAsync(IReadOnlyList<object> saves,IReadOnlyList<object> deletes,CancellationToken cancellationToken=default)
        {foreach(var d in deletes)Items.Remove(d);foreach(var s in saves)if(!Items.Contains(s))Items.Add(s);return Task.CompletedTask;}
    }
    [Fact]
    public async Task CatalogueCanBeAddedRepeatedlyWithoutReplacingSchoolSubjects()
    {
        var store=new Store();var subject=new Subject("Our Mathematics","MATH");store.Items.Add(subject);
        var vm=new ReferenceDataViewModel(store,NullLogger<ReferenceDataViewModel>.Instance);
        await vm.AddSubjectCatalogueCommand.ExecuteAsync(null);var count=vm.Subjects.Count;
        await vm.AddSubjectCatalogueCommand.ExecuteAsync(null);
        Assert.Empty(vm.Error);Assert.True(count>=35);Assert.Equal(count,vm.Subjects.Count);
        Assert.Equal(subject.Id,Assert.Single(vm.Subjects,s=>s.SubjectCode=="MATH").Id);Assert.Equal("Our Mathematics",subject.Name);
    }
    [Fact]
    public async Task ExamFormSavesMultipleVenuesAndRejectsConflictsAndOverCapacity()
    {
        var store=new Store();var subject=new Subject("Maths","M");var a=new Venue("A",30);var b=new Venue("B",30);store.Items.AddRange([subject,a,b]);
        var vm=new ExaminationWorkspaceViewModel(store,NullLogger<ExaminationWorkspaceViewModel>.Instance);
        await vm.LoadCommand.ExecuteAsync(null);vm.SelectedSubject=subject;vm.Grade=8;vm.Date=new(2026,12,10);vm.StartTime="08:00";vm.EndTime="10:00";
        vm.Allocations.Add(new VenueAllocationRow(a.Id,31,1));await vm.SaveCommand.ExecuteAsync(null);Assert.NotEmpty(vm.Error);Assert.Empty(store.Items.OfType<Exam>());
        vm.Allocations[0].StudentCount=30;vm.Allocations.Add(new(b.Id,20,2));await vm.SaveCommand.ExecuteAsync(null);
        Assert.Empty(vm.Error);Assert.Single(store.Items.OfType<Exam>());Assert.Equal(2,store.Items.OfType<ExamVenue>().Count());
        vm.CopyCommand.Execute(null);await vm.SaveCommand.ExecuteAsync(null);Assert.NotEmpty(vm.Error);Assert.Single(store.Items.OfType<Exam>());
    }
    [Fact]
    public async Task ReferenceFormsValidateAndSave()
    {
        var store=new Store();var vm=new ReferenceDataViewModel(store,NullLogger<ReferenceDataViewModel>.Instance);
        await vm.SaveSubjectCommand.ExecuteAsync(null);Assert.NotEmpty(vm.Error);
        vm.SubjectName="English";vm.SubjectCode="ENG";await vm.SaveSubjectCommand.ExecuteAsync(null);Assert.Empty(vm.Error);Assert.Single(store.Items.OfType<Subject>());
        vm.VenueName="A1";vm.Capacity=0;await vm.SaveVenueCommand.ExecuteAsync(null);Assert.NotEmpty(vm.Error);
        vm.Capacity=40;await vm.SaveVenueCommand.ExecuteAsync(null);Assert.Empty(vm.Error);Assert.Single(store.Items.OfType<Venue>());
    }
}
