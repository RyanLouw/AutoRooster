using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Application;
public sealed record WorkloadChartRow(string Teacher,decimal Weight,int AvailableMinutes,double TargetMinutes,int ActualMinutes,double Difference,double? PercentageOfTarget,int AssignmentCount,int DaysAssigned,double ActualWidth,double TargetWidth)
{
    public bool LargeDeviation=>TargetMinutes>0 && Math.Abs(Difference)/TargetMinutes>0.25;
    public double ActualHours=>ActualMinutes/60.0;
    public double TargetHours=>TargetMinutes/60.0;
    public string Duration=>ReportTime.Duration(ActualMinutes);
}
public partial class ReportsViewModel(ISchoolDataStore store,ReportingService service,IReportExporter exporter,IFileDialogs files,ILogger<ReportsViewModel> logger) : WorkspaceViewModel(logger)
{
    public override Task RefreshAsync()=>LoadCommand.IsRunning ? LoadCommand.ExecutionTask??Task.CompletedTask : LoadCommand.ExecuteAsync(null);
    public ObservableCollection<RosterVersion> Rosters {get;}=[];
    public IEnumerable<RosterVersion> VisibleRosters=>Rosters.Where(r=>ShowPreviousSeries || !r.IsArchived);
    [ObservableProperty] private bool showPreviousSeries;
    partial void OnShowPreviousSeriesChanged(bool value)
    {
        OnPropertyChanged(nameof(VisibleRosters));
        if(SelectedRoster is null || (!value && SelectedRoster.IsArchived))SelectedRoster=VisibleRosters.FirstOrDefault();
    }
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
    private bool loadingVersions;
    partial void OnSelectedRosterChanged(RosterVersion? value)
    {
        if(!loadingVersions)BuildReportCommand.Execute(null);
    }
    [RelayCommand] private Task LoadAsync()=>RunAsync(async()=>
    {
        var id=SelectedRoster?.Id;
        var versions=(await store.ListAsync<RosterVersion>()).OrderByDescending(x=>x.CreatedOn).ToArray();
        loadingVersions=true;
        try
        {
            Rosters.Clear();foreach(var roster in versions)Rosters.Add(roster);
            OnPropertyChanged(nameof(VisibleRosters));SelectedRoster=VisibleRosters.FirstOrDefault(x=>x.Id==id)??VisibleRosters.FirstOrDefault();
        }
        finally{loadingVersions=false;}
        Teachers.Clear();foreach(var teacher in await store.ListAsync<Teacher>())Teachers.Add(teacher);
        await RefreshReportAsync();
    });
    [RelayCommand] private Task BuildReportAsync()=>RunAsync(RefreshReportAsync);
    private async Task RefreshReportAsync()
    {
        var id=SelectedRoster?.Id;
        Report=null;Workloads.Clear();ExportStatus="";RefreshFilters();
        if(id is null)return;
        var snapshot=await service.BuildAsync(id.Value);
        // A slower previous selection must never replace the newly selected report.
        if(SelectedRoster?.Id!=id)return;
        Report=snapshot;var scale=Report.Fairness.Teachers.Select(x=>Math.Max(x.TargetMinutes,x.ActualMinutes)).DefaultIfEmpty(1).Max();scale=Math.Max(1,scale);
        foreach(var x in Report.Fairness.Teachers.OrderByDescending(x=>x.ActualMinutes))Workloads.Add(new(Report.TeacherNames[x.TeacherId],x.Weight,x.AvailableMinutes,x.TargetMinutes,x.ActualMinutes,x.Difference,x.PercentageOfTarget,x.AssignmentCount,x.DaysAssigned,280*x.ActualMinutes/scale,280*x.TargetMinutes/scale));
        RefreshFilters();
    }
    public IReadOnlyList<ReportExportOption> ExportOptions {get;}=
    [new(ReportKind.AllReports,"All reports (one PDF)"),new(ReportKind.Workload,"Teacher hours — whole exam period"),new(ReportKind.TeacherTimetables,"All teacher timetables"),new(ReportKind.TeacherRoster,"Selected teacher timetable"),new(ReportKind.FullRoster,"Full examination roster"),new(ReportKind.VenueRoster,"Venue roster"),new(ReportKind.Calendar,"Calendar / timeline"),new(ReportKind.UnfilledDuties,"Unfilled duties")];
    [ObservableProperty] private ReportKind exportKind=ReportKind.AllReports;
    [ObservableProperty] private string exportStatus="";
    [RelayCommand] private Task ExportExcelAsync()=>RunAsync(async()=>
    {
        if(SelectedRoster is null)throw new ArgumentException("Select a roster version.");
        var path=await files.SavePathAsync("Export complete Excel workbook","Excel workbook|*.xlsx",".xlsx");if(path is null)return;
        await exporter.ExportExcelAsync(await service.BuildAsync(SelectedRoster.Id),path);ExportStatus="Excel workbook saved.";
    });
    [RelayCommand] private Task ExportAllAsync()=>RunAsync(async()=>
    {
        if(SelectedRoster is null)throw new ArgumentException("Select a roster version.");
        var path=await files.SavePathAsync("Export all reports and teacher timetables","Report ZIP|*.zip",".zip");if(path is null)return;
        await exporter.ExportAllAsync(await service.BuildAsync(SelectedRoster.Id),path);
        ExportStatus="All reports saved: Excel workbook, combined and separate PDFs, and an individual PDF for every teacher.";
    });
    [RelayCommand] private Task ExportTeacherHoursAsync()=>RunAsync(()=>ExportPdfCoreAsync(ReportKind.Workload));
    [RelayCommand] private Task ExportPdfAsync()=>RunAsync(()=>ExportPdfCoreAsync(ExportKind));
    private async Task ExportPdfCoreAsync(ReportKind kind)
    {
        if(SelectedRoster is null)throw new ArgumentException("Select a roster version.");
        if(kind==ReportKind.TeacherRoster && SelectedTeacher is null)throw new ArgumentException("Select a teacher in the roster tab first.");
        var path=await files.SavePathAsync("Export PDF report","PDF document|*.pdf",".pdf");if(path is null)return;
        await exporter.ExportPdfAsync(await service.BuildAsync(SelectedRoster.Id),path,kind,SelectedTeacher?.Id);ExportStatus="PDF report saved.";
    }
}
public sealed record ReportExportOption(ReportKind Kind,string Label);
