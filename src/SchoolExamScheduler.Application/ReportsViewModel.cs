using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Application;
public sealed record WorkloadChartRow(string Teacher,decimal Weight,int AvailableMinutes,double TargetMinutes,int ActualMinutes,double Difference,double? PercentageOfTarget,int AssignmentCount,int DaysAssigned,double ActualWidth,double TargetWidth)
{public bool LargeDeviation=>TargetMinutes>0 && Math.Abs(Difference)/TargetMinutes>0.25;}
public partial class ReportsViewModel(ISchoolDataStore store,ReportingService service,IReportExporter exporter,IFileDialogs files,ILogger<ReportsViewModel> logger) : WorkspaceViewModel(logger)
{
    public ObservableCollection<RosterVersion> Rosters {get;}=[];
    public ObservableCollection<Teacher> Teachers {get;}=[];
    public ObservableCollection<WorkloadChartRow> Workloads {get;}=[];
    [ObservableProperty] private RosterVersion? selectedRoster;
    [ObservableProperty] private ReportSnapshot? report;
    [ObservableProperty] private Teacher? selectedTeacher;
    [ObservableProperty] private DateTime selectedDate=DateTime.Today;
    [ObservableProperty] private bool filterDate;
    [ObservableProperty] private string search="";
    public IEnumerable<DutyRow> FilteredDuties=>(Report?.Duties??[]).Where(x=>(SelectedTeacher is null || x.TeacherId==SelectedTeacher.Id) && (!FilterDate || x.Date==DateOnly.FromDateTime(SelectedDate))).Where(x=>$"{x.Teacher} {x.Examination} {x.Venue}".Contains(Search,StringComparison.CurrentCultureIgnoreCase)).OrderBy(x=>x.Date).ThenBy(x=>x.Start).ThenBy(x=>x.Examination).ThenBy(x=>x.Venue);
    public int FilteredMinutes=>FilteredDuties.Sum(x=>x.Minutes);
    private void RefreshFilters(){OnPropertyChanged(nameof(FilteredDuties));OnPropertyChanged(nameof(FilteredMinutes));}
    partial void OnSelectedTeacherChanged(Teacher? value)=>RefreshFilters();
    partial void OnSelectedDateChanged(DateTime value)=>RefreshFilters();
    partial void OnFilterDateChanged(bool value)=>RefreshFilters();
    partial void OnSearchChanged(string value)=>RefreshFilters();
    [RelayCommand] private void ClearFilters(){SelectedTeacher=null;FilterDate=false;Search="";}
    [RelayCommand] private Task LoadAsync()=>RunAsync(async()=>
    {
        var id=SelectedRoster?.Id;Rosters.Clear();foreach(var roster in (await store.ListAsync<RosterVersion>()).OrderByDescending(x=>x.CreatedOn))Rosters.Add(roster);SelectedRoster=Rosters.FirstOrDefault(x=>x.Id==id)??Rosters.FirstOrDefault();
        Teachers.Clear();foreach(var teacher in await store.ListAsync<Teacher>())Teachers.Add(teacher);
    });
    [RelayCommand] private Task BuildReportAsync()=>RunAsync(async()=>
    {
        if(SelectedRoster is null)throw new ArgumentException("Select a roster version.");
        Report=await service.BuildAsync(SelectedRoster.Id);Workloads.Clear();var scale=Report.Fairness.Teachers.Select(x=>Math.Max(x.TargetMinutes,x.ActualMinutes)).DefaultIfEmpty(1).Max();scale=Math.Max(1,scale);
        foreach(var x in Report.Fairness.Teachers.OrderByDescending(x=>x.ActualMinutes))Workloads.Add(new(Report.TeacherNames[x.TeacherId],x.Weight,x.AvailableMinutes,x.TargetMinutes,x.ActualMinutes,x.Difference,x.PercentageOfTarget,x.AssignmentCount,x.DaysAssigned,280*x.ActualMinutes/scale,280*x.TargetMinutes/scale));
        RefreshFilters();
    });
    public IReadOnlyList<ReportKind> ExportKinds {get;}=Enum.GetValues<ReportKind>();
    [ObservableProperty] private ReportKind exportKind;
    [ObservableProperty] private string exportStatus="";
    [RelayCommand] private Task ExportExcelAsync()=>RunAsync(async()=>
    {
        if(SelectedRoster is null)throw new ArgumentException("Select a roster version.");
        var path=await files.SavePathAsync("Export complete Excel workbook","Excel workbook|*.xlsx",".xlsx");if(path is null)return;
        await exporter.ExportExcelAsync(await service.BuildAsync(SelectedRoster.Id),path);ExportStatus="Excel workbook saved.";
    });
    [RelayCommand] private Task ExportPdfAsync()=>RunAsync(async()=>
    {
        if(SelectedRoster is null)throw new ArgumentException("Select a roster version.");
        if(ExportKind==ReportKind.TeacherRoster && SelectedTeacher is null)throw new ArgumentException("Select a teacher in the roster tab first.");
        var path=await files.SavePathAsync("Export PDF report","PDF document|*.pdf",".pdf");if(path is null)return;
        await exporter.ExportPdfAsync(await service.BuildAsync(SelectedRoster.Id),path,ExportKind,SelectedTeacher?.Id);ExportStatus="PDF report saved.";
    });

}
