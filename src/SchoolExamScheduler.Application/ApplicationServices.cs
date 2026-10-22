using Microsoft.Extensions.DependencyInjection;

namespace SchoolExamScheduler.Application;

public sealed record ApplicationIdentity(string Name);

public static class ApplicationServices
{
    public static IServiceCollection AddSchedulerApplication(this IServiceCollection services)
    {
        services.AddSingleton(new ApplicationIdentity("School Exam Invigilation Scheduler"));
        return services;
    }
}
