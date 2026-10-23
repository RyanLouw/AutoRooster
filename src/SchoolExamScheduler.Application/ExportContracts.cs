namespace SchoolExamScheduler.Application;
public enum ReportKind { FullRoster,TeacherRoster,Workload,VenueRoster,TeacherTimetables,Calendar,UnfilledDuties,AllReports }
public interface IReportExporter
{
    Task ExportAllAsync(ReportSnapshot report,string path,CancellationToken cancellationToken=default);
    Task ExportExcelAsync(ReportSnapshot report,string path,CancellationToken cancellationToken=default);
    Task ExportPdfAsync(ReportSnapshot report,string path,ReportKind kind,Guid? teacherId=null,CancellationToken cancellationToken=default);
}
public interface IFileDialogs
{
    Task<string?> SavePathAsync(string title,string filter,string extension);
    Task<string?> OpenPathAsync(string title,string filter);
}
