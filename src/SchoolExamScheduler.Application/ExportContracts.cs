namespace SchoolExamScheduler.Application;
public enum ReportKind { FullRoster,TeacherRoster,Workload,VenueRoster }
public interface IReportExporter
{
    Task ExportExcelAsync(ReportSnapshot report,string path,CancellationToken cancellationToken=default);
    Task ExportPdfAsync(ReportSnapshot report,string path,ReportKind kind,Guid? teacherId=null,CancellationToken cancellationToken=default);
}
public interface IFileDialogs
{
    Task<string?> SavePathAsync(string title,string filter,string extension);
    Task<string?> OpenPathAsync(string title,string filter);
}
