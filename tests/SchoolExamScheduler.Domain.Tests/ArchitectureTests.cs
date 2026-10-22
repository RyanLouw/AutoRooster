using Xunit;
public sealed class ArchitectureTests
{
    [Fact]
    public void DomainHasNoFrameworkDatabaseOrPresentationDependency()
    {
        var assembly = System.Reflection.Assembly.Load("SchoolExamScheduler.Domain");
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name!.Contains("EntityFramework") || a.Name.Contains("PresentationFramework") || a.Name.Contains("SchoolExamScheduler.Application"));
    }
}
