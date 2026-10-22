using Microsoft.Extensions.Logging.Abstractions;
using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using Xunit;

public sealed class TeacherWorkspaceTests
{
    private sealed class Store : ISchoolDataStore
    {
        public List<object> Items {get;}=[];
        public Task CommitAsync(IReadOnlyList<object> saves,IReadOnlyList<object> deletes,CancellationToken cancellationToken=default)
        {foreach(var d in deletes)Items.Remove(d);foreach(var s in saves)if(!Items.Contains(s))Items.Add(s);return Task.CompletedTask;}
        public Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken=default) where T:class => Task.FromResult<IReadOnlyList<T>>(Items.OfType<T>().ToArray());
        public Task SaveAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class {if(!Items.Contains(entity))Items.Add(entity);return Task.CompletedTask;}
        public Task DeleteAsync<T>(T entity,CancellationToken cancellationToken=default) where T:class {Items.Remove(entity);return Task.CompletedTask;}
    }
    private sealed class Dialogs(bool answer=true) : IUserDialogs
    { public Task<bool> ConfirmAsync(string message)=>Task.FromResult(answer); }
    [Fact]
    public async Task TeacherFormValidatesAndSavesAndSearches()
    {
        var store=new Store();var vm=new TeacherWorkspaceViewModel(store,new Dialogs(),NullLogger<TeacherWorkspaceViewModel>.Instance);
        await vm.SaveCommand.ExecuteAsync(null);Assert.NotEmpty(vm.Error);Assert.Empty(store.Items);
        vm.FirstName="Ada";vm.LastName="Zulu";vm.WorkloadWeight=0.5m;
        await vm.SaveCommand.ExecuteAsync(null);Assert.Empty(vm.Error);Assert.Single(vm.Teachers);
        var teacher=Assert.IsType<Teacher>(Assert.Single(store.Items));Assert.Equal(0.5m,teacher.WorkloadWeight);
        vm.Search="Nobody";Assert.Empty(vm.FilteredTeachers);vm.Search="Ada";Assert.Single(vm.FilteredTeachers);
        vm.SelectedTeacher=teacher;vm.LastName="Updated";await vm.SaveCommand.ExecuteAsync(null);Assert.Equal("Updated",teacher.LastName);
    }
    [Fact]
    public async Task DeactivateRequiresConfirmation()
    {
        var store=new Store();var teacher=new Teacher("A","B");store.Items.Add(teacher);
        var vm=new TeacherWorkspaceViewModel(store,new Dialogs(false),NullLogger<TeacherWorkspaceViewModel>.Instance){SelectedTeacher=teacher};
        await vm.DeactivateCommand.ExecuteAsync(null);Assert.True(teacher.IsActive);
        vm=new(store,new Dialogs(),NullLogger<TeacherWorkspaceViewModel>.Instance){SelectedTeacher=teacher};
        await vm.DeactivateCommand.ExecuteAsync(null);Assert.False(teacher.IsActive);
    }
    [Fact]
    public async Task AvailabilityAddsEditsAndRemovesWithValidation()
    {
        var store=new Store();var teacher=new Teacher("A","B");store.Items.Add(teacher);
        var vm=new AvailabilityWorkspaceViewModel(store,new Dialogs(),NullLogger<AvailabilityWorkspaceViewModel>.Instance);
        await vm.LoadCommand.ExecuteAsync(null);vm.SelectedTeacher=teacher;vm.Date=new(2026,12,10);vm.StartTime="10:00";vm.EndTime="09:00";
        await vm.SaveCommand.ExecuteAsync(null);Assert.NotEmpty(vm.Error);Assert.Empty(store.Items.OfType<TeacherAvailability>());
        vm.StartTime="08:00";vm.EndTime="09:00";await vm.SaveCommand.ExecuteAsync(null);Assert.Empty(vm.Error);Assert.Single(vm.Records);
        vm.SelectedRecord=vm.Records[0];vm.Reason="Leave";await vm.SaveCommand.ExecuteAsync(null);Assert.Equal("Leave",Assert.Single(vm.Records).Reason);
        await vm.RemoveCommand.ExecuteAsync(null);Assert.Empty(vm.Records);
    }
}
