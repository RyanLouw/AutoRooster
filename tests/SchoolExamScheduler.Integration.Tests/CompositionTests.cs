using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchoolExamScheduler.Application;
using Xunit;
public sealed class CompositionTests
{
    [Fact]
    public void CompositionProvidesSingleApplicationIdentity()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSchedulerApplication();
        using var host = builder.Build();
        Assert.Same(host.Services.GetRequiredService<ApplicationIdentity>(), host.Services.GetRequiredService<ApplicationIdentity>());
        Assert.Single(host.Services.GetServices<ApplicationIdentity>());
    }
}
