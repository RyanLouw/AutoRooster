using System.Windows;
using SchoolExamScheduler.Application;
namespace SchoolExamScheduler.Desktop;
public partial class MainWindow : Window
{
    public MainWindow(TeacherWorkspaceViewModel teachers,AvailabilityWorkspaceViewModel availability,ReferenceDataViewModel referenceData,ExaminationWorkspaceViewModel examinations,GenerationWorkspaceViewModel generation,ReviewRosterViewModel review,ReportsViewModel reports,SettingsViewModel settings)
    {
        InitializeComponent();DataContext=new {Teachers=teachers,Availability=availability,ReferenceData=referenceData,Examinations=examinations,Generation=generation,Review=review,Reports=reports,Settings=settings};
        settings.DatabaseRestored+=Close;
        Loaded+=async (_,_)=>await RefreshActiveWorkspaceAsync();
    }
    private async void WorkspaceChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Ignore selection changes bubbling from grids, combo boxes and nested tabs.
        if(ReferenceEquals(e.OriginalSource,Workspaces) && IsLoaded)await RefreshActiveWorkspaceAsync();
    }
    private Task RefreshActiveWorkspaceAsync()
    {
        if(Workspaces.SelectedItem is System.Windows.Controls.TabItem {Content:FrameworkElement {DataContext:WorkspaceViewModel workspace}} && !workspace.IsBusy)
            return workspace.RefreshAsync();
        return Task.CompletedTask;
    }
}
