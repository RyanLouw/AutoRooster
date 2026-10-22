using Xunit;
public sealed class ArchitectureTests
{
    [Fact]
    public void SchedulingHasNoDatabaseOrPresentationDependency()
    {
        var assembly = System.Reflection.Assembly.Load("SchoolExamScheduler.Scheduling");
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name!.Contains("EntityFramework") || a.Name.Contains("PresentationFramework") || a.Name.Contains("Infrastructure"));
    }
}
