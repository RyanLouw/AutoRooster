using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Application;
using Xunit;

public sealed class StartupTests
{
    [Fact]
    public async Task HostStartsResolvesServicesAndShutsDown()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSchedulerApplication();
        using var host = builder.Build();
        await host.StartAsync();
        Assert.NotNull(host.Services.GetRequiredService<ILogger<StartupTests>>());
        Assert.Equal("School Exam Invigilation Scheduler", host.Services.GetRequiredService<ApplicationIdentity>().Name);
        await host.StopAsync();
    }
}
