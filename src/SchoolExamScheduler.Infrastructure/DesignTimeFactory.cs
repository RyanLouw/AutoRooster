using Microsoft.EntityFrameworkCore.Design;
namespace SchoolExamScheduler.Infrastructure;
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<SchedulerDbContext>
{
    public SchedulerDbContext CreateDbContext(string[] args) => SchedulerDbContext.Open(Path.Combine(Path.GetTempPath(),"SchoolExamScheduler-design","design.db"));
}
