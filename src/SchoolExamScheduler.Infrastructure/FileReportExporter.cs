using ClosedXML.Excel;
using System.IO.Compression;
using SchoolExamScheduler.Application;
namespace SchoolExamScheduler.Infrastructure;
public sealed partial class FileReportExporter : IReportExporter
{
    private static Task AtomicWriteAsync(string path,Action<string> write,CancellationToken cancellationToken)=>
        AtomicWriteCoreAsync(path,temp=>Task.Run(()=>write(temp),cancellationToken),cancellationToken);
    private static async Task AtomicWriteCoreAsync(string path,Func<string,Task> write,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory=Path.GetDirectoryName(Path.GetFullPath(path))!;Directory.CreateDirectory(directory);
        var temporary=Path.Combine(directory,"."+Guid.NewGuid()+Path.GetExtension(path));
        try
        {
            if(File.Exists(path))throw new IOException("The destination already exists. Choose a new filename to preserve it.");
            await write(temporary);cancellationToken.ThrowIfCancellationRequested();File.Move(temporary,path,false);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public Task ExportAllAsync(ReportSnapshot report,string path,CancellationToken cancellationToken=default)=>AtomicWriteCoreAsync(path,async temp=>
    {
        var folder=Path.Combine(Path.GetTempPath(),"school-reports-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        try
        {
            await ExportExcelAsync(report,Path.Combine(folder,"Reports.xlsx"),cancellationToken);
            (ReportKind Kind,string File)[] reports=[(ReportKind.AllReports,"All-reports.pdf"),(ReportKind.FullRoster,"Full-roster.pdf"),(ReportKind.Workload,"Teacher-hours.pdf"),(ReportKind.TeacherTimetables,"Teacher-timetables.pdf"),(ReportKind.VenueRoster,"Venue-roster.pdf"),(ReportKind.Calendar,"Calendar.pdf"),(ReportKind.UnfilledDuties,"Unfilled-duties.pdf")];
            foreach(var item in reports)await ExportPdfAsync(report,Path.Combine(folder,item.File),item.Kind,cancellationToken:cancellationToken);
            int index=0;
            foreach(var teacher in report.TeacherHours)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name=new string(teacher.Teacher.Where(c=>char.IsLetterOrDigit(c)||c is ' ' or '-').Take(80).ToArray()).Trim();
                if(name.Length==0)name="Teacher";
                await ExportPdfAsync(report,Path.Combine(folder,"Teachers",$"{++index:000}-{name}.pdf"),ReportKind.TeacherRoster,teacher.TeacherId,cancellationToken);
            }
            await Task.Run(()=>ZipFile.CreateFromDirectory(folder,temp,CompressionLevel.Optimal,false),cancellationToken);
        }
        finally{Directory.Delete(folder,true);}
    },cancellationToken);
    public Task ExportExcelAsync(ReportSnapshot report,string path,CancellationToken cancellationToken=default)=>AtomicWriteAsync(path,temp=>
    {
        using var workbook=new XLWorkbook();
        void Roster(string name,IEnumerable<DutyRow> data)
        {
            var rows=data.ToArray();var sheet=workbook.Worksheets.Add(name);
            string[] headers=["Date","Examination","Venue","Position","Start","End","Teacher","Minutes","Hours","Session"];
            for(int c=0;c<headers.Length;c++)sheet.Cell(1,c+1).Value=headers[c];
            for(int i=0;i<rows.Length;i++)
            {
                cancellationToken.ThrowIfCancellationRequested();var row=rows[i];int r=i+2;
                sheet.Cell(r,1).Value=row.Date.ToDateTime(TimeOnly.MinValue);sheet.Cell(r,1).Style.DateFormat.Format="yyyy-mm-dd";
                sheet.Cell(r,2).Value=row.Examination;sheet.Cell(r,3).Value=row.Venue;sheet.Cell(r,4).Value=row.Position;
                sheet.Cell(r,5).Value=row.Start.ToTimeSpan();sheet.Cell(r,6).Value=row.End.ToTimeSpan();sheet.Range(r,5,r,6).Style.DateFormat.Format="hh:mm";
                sheet.Cell(r,7).Value=row.Teacher;sheet.Cell(r,8).Value=row.Minutes;sheet.Cell(r,9).Value=row.Hours;sheet.Cell(r,9).Style.NumberFormat.Format="0.00";sheet.Cell(r,10).Value=row.Session;
            }
            sheet.Cell(rows.Length+2,7).Value="Total minutes";sheet.Cell(rows.Length+2,8).Value=rows.Sum(x=>x.Minutes);sheet.Cell(rows.Length+2,9).Value=rows.Sum(x=>x.Minutes)/60.0;Format(sheet,rows.Length+1,10);
        }
        Roster("Full roster",report.Duties);
        int index=0;foreach(var teacher in report.TeacherNames.OrderBy(x=>x.Value))Roster($"Teacher {++index:000}",report.Duties.Where(x=>x.TeacherId==teacher.Key));
        Roster("Venue roster",report.Duties.OrderBy(x=>x.Venue).ThenBy(x=>x.Date).ThenBy(x=>x.Start));
        var hours=workbook.Worksheets.Add("Teacher hours");var dates=report.Dates;
        hours.Cell(1,1).Value="Teacher";hours.Cell(1,2).Value="Total hours";hours.Cell(1,3).Value="Total (hh:mm)";
        for(int d=0;d<dates.Count;d++){hours.Cell(1,d+4).Value=dates[d].ToDateTime(TimeOnly.MinValue);hours.Cell(1,d+4).Style.DateFormat.Format="yyyy-mm-dd";}
        int hr=2;
        foreach(var teacher in report.TeacherHours)
        {
            hours.Cell(hr,1).Value=teacher.Teacher;hours.Cell(hr,2).Value=teacher.Hours;hours.Cell(hr,3).Value=teacher.Duration;
            for(int d=0;d<dates.Count;d++)hours.Cell(hr,d+4).Value=report.Duties.Where(x=>x.TeacherId==teacher.TeacherId && x.Date==dates[d]).Sum(x=>x.Minutes)/60.0;
            hr++;
        }
        hours.Cell(hr,1).Value="Total";hours.Cell(hr,2).Value=report.AssignedHours;hours.Cell(hr,3).Value=ReportTime.Duration(report.AssignedMinutes);
        for(int d=0;d<dates.Count;d++)hours.Cell(hr,d+4).Value=report.Duties.Where(x=>x.Date==dates[d]).Sum(x=>x.Minutes)/60.0;
        hours.Column(2).Style.NumberFormat.Format="0.00";if(dates.Count>0)hours.Columns(4,dates.Count+3).Style.NumberFormat.Format="0.00";Format(hours,hr-1,dates.Count+3);
        var timeline=workbook.Worksheets.Add("Teacher timeline");string[] th=["Teacher","Date","Examination","Venue / position","Start","End","Duration (hh:mm)","Hours","Session"];
        for(int c=0;c<th.Length;c++)timeline.Cell(1,c+1).Value=th[c];int tr=2;
        foreach(var duty in report.Duties.OrderBy(x=>x.Teacher,StringComparer.CurrentCulture).ThenBy(x=>x.TeacherId).ThenBy(x=>x.Date).ThenBy(x=>x.Start))
        {
            timeline.Cell(tr,1).Value=duty.Teacher;timeline.Cell(tr,2).Value=duty.Date.ToDateTime(TimeOnly.MinValue);timeline.Cell(tr,2).Style.DateFormat.Format="yyyy-mm-dd";
            timeline.Cell(tr,3).Value=duty.Examination;timeline.Cell(tr,4).Value=$"{duty.Venue} / position {duty.Position}";
            timeline.Cell(tr,5).Value=duty.Start.ToTimeSpan();timeline.Cell(tr,6).Value=duty.End.ToTimeSpan();timeline.Range(tr,5,tr,6).Style.DateFormat.Format="hh:mm";
            timeline.Cell(tr,7).Value=duty.Duration;timeline.Cell(tr,8).Value=duty.Hours;timeline.Cell(tr,8).Style.NumberFormat.Format="0.00";timeline.Cell(tr,9).Value=duty.Session;tr++;
        }
        Format(timeline,tr-1,9);
        Roster("Calendar",report.Duties.OrderBy(x=>x.Date).ThenBy(x=>x.Start).ThenBy(x=>x.Venue).ThenBy(x=>x.Position));
        var workload=workbook.Worksheets.Add("Workload");string[] wh=["Teacher","Weight","Eligible minutes","Target minutes","Actual minutes","Difference","Percent of target","Assignments","Days"];
        for(int c=0;c<wh.Length;c++)workload.Cell(1,c+1).Value=wh[c];int n=2;
        foreach(var row in report.Fairness.Teachers)
        {
            workload.Cell(n,1).Value=report.TeacherNames[row.TeacherId];workload.Cell(n,2).Value=(double)row.Weight;workload.Cell(n,3).Value=row.AvailableMinutes;workload.Cell(n,4).Value=row.TargetMinutes;workload.Cell(n,5).Value=row.ActualMinutes;workload.Cell(n,6).Value=row.Difference;
            if(row.PercentageOfTarget is double percentage){workload.Cell(n,7).Value=percentage/100;workload.Cell(n,7).Style.NumberFormat.Format="0.0%";}
            workload.Cell(n,8).Value=row.AssignmentCount;workload.Cell(n,9).Value=row.DaysAssigned;n++;
        }
        workload.Cell(n,1).Value="Total";workload.Cell(n,4).Value=report.Fairness.Teachers.Sum(x=>x.TargetMinutes);workload.Cell(n,5).Value=report.AssignedMinutes;
        Format(workload,n-1,9);
        var unfilled=workbook.Worksheets.Add("Unfilled duties");string[] uh=["Date","Examination","Venue","Position","Start","End","Missing minutes"];
        for(int c=0;c<uh.Length;c++)unfilled.Cell(1,c+1).Value=uh[c];n=2;
        foreach(var row in report.Unfilled)
        {
            unfilled.Cell(n,1).Value=row.Date.ToDateTime(TimeOnly.MinValue);unfilled.Cell(n,1).Style.DateFormat.Format="yyyy-mm-dd";unfilled.Cell(n,2).Value=row.Examination;unfilled.Cell(n,3).Value=row.Venue;unfilled.Cell(n,4).Value=row.Position;
            unfilled.Cell(n,5).Value=row.Start.ToTimeSpan();unfilled.Cell(n,6).Value=row.End.ToTimeSpan();unfilled.Range(n,5,n,6).Style.DateFormat.Format="hh:mm";unfilled.Cell(n,7).Value=row.Minutes;n++;
        }
        Format(unfilled,n-1,7);
        workbook.Properties.Title=$"{report.SchoolName} — {report.RosterName} — {report.Period} ({report.Status})";
        workbook.SaveAs(temp);
    },cancellationToken);
    private static void Format(IXLWorksheet sheet,int lastRow,int columns)
    {
        sheet.Range(1,1,1,columns).Style.Font.Bold=true;sheet.Range(1,1,1,columns).Style.Fill.BackgroundColor=XLColor.FromHtml("#DDEAF4");
        sheet.SheetView.FreezeRows(1);sheet.Range(1,1,Math.Max(1,lastRow),columns).SetAutoFilter();sheet.Columns().AdjustToContents(8,48);
        sheet.Style.Alignment.WrapText=true;sheet.PageSetup.PageOrientation=XLPageOrientation.Landscape;sheet.PageSetup.FitToPages(1,0);sheet.PageSetup.SetRowsToRepeatAtTop(1,1);
    }
}
