using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Application;
using SchoolExamScheduler.Infrastructure;

namespace SchoolExamScheduler.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var builder = Host.CreateApplicationBuilder(e.Args);
            builder.Services.AddSchedulerApplication();
            var dataDirectory=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SchoolExamScheduler");
            var databasePath=System.IO.Path.Combine(dataDirectory,"school-exams.db");
            builder.Logging.AddProvider(new JsonFileLoggerProvider(System.IO.Path.Combine(dataDirectory,"logs")));
            await using(var database=SchedulerDbContext.Open(databasePath))await database.InitializeAsync();
            var access=new DatabaseAccess();
            builder.Services.AddSingleton<ISchoolDataStore>(new SchoolDataStore(databasePath,access));
            builder.Services.AddSingleton<IDatabaseMaintenance>(new DatabaseMaintenance(databasePath,access));
            builder.Services.AddSingleton<IUserDialogs,UserDialogs>();
            builder.Services.AddTransient<TeacherWorkspaceViewModel>();
            builder.Services.AddTransient<AvailabilityWorkspaceViewModel>();
            builder.Services.AddTransient<ReferenceDataViewModel>();
            builder.Services.AddTransient<ExaminationWorkspaceViewModel>();
            builder.Services.AddSingleton<SchoolExamScheduler.Scheduling.InvigilationSolver>();
            builder.Services.AddTransient<RosterWorkflow>();
            builder.Services.AddTransient<GenerationWorkspaceViewModel>();
            builder.Services.AddTransient<ManualRosterService>();
            builder.Services.AddTransient<ReviewRosterViewModel>();
            builder.Services.AddTransient<ReportingService>();
            builder.Services.AddSingleton<IReportExporter,FileReportExporter>();
            builder.Services.AddSingleton<IFileDialogs,FileDialogs>();
            builder.Services.AddTransient<ReportsViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddSingleton<MainWindow>();
            host = builder.Build();
            await host.StartAsync();
            host.Services.GetRequiredService<ILogger<App>>().LogInformation("Application started");
            MainWindow = host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        catch (Exception exception)
        {
            host?.Services.GetService<ILogger<App>>()?.LogCritical(exception, "Startup failed");
            MessageBox.Show($"The application could not start.\n{exception.Message}", "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (host is not null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await host.StopAsync(timeout.Token);
            }
        }
        catch (Exception exception)
        {
            host?.Services.GetService<ILogger<App>>()?.LogError(exception, "Shutdown failed");
        }
        finally
        {
            host?.Dispose();
            base.OnExit(e);
        }
    }
}
