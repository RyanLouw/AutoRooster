using System.Windows;
using SchoolExamScheduler.Application;
namespace SchoolExamScheduler.Desktop;
public partial class MainWindow : Window
{
    public MainWindow(TeacherWorkspaceViewModel teachers,AvailabilityWorkspaceViewModel availability,ReferenceDataViewModel referenceData,ExaminationWorkspaceViewModel examinations,GenerationWorkspaceViewModel generation,ReviewRosterViewModel review,ReportsViewModel reports,SettingsViewModel settings)
    {
        InitializeComponent();DataContext=new {Teachers=teachers,Availability=availability,ReferenceData=referenceData,Examinations=examinations,Generation=generation,Review=review,Reports=reports,Settings=settings};
        settings.DatabaseRestored+=Close;
        Loaded+=async (_,_)=>{await teachers.LoadCommand.ExecuteAsync(null);await availability.LoadCommand.ExecuteAsync(null);await referenceData.LoadCommand.ExecuteAsync(null);await examinations.LoadCommand.ExecuteAsync(null);await review.LoadCommand.ExecuteAsync(null);await reports.LoadCommand.ExecuteAsync(null);await settings.LoadCommand.ExecuteAsync(null);};
    }
}
