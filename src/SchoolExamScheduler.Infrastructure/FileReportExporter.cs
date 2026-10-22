using ClosedXML.Excel;
using SchoolExamScheduler.Application;
namespace SchoolExamScheduler.Infrastructure;
public sealed partial class FileReportExporter : IReportExporter
{
    private static async Task AtomicWriteAsync(string path,Action<string> write,CancellationToken cancellationToken)
    {
        var directory=Path.GetDirectoryName(Path.GetFullPath(path))!;Directory.CreateDirectory(directory);
        var temporary=Path.Combine(directory,"."+Guid.NewGuid()+Path.GetExtension(path));
        try
        {
            if(File.Exists(path))throw new IOException("The destination already exists. Choose a new filename to preserve it.");
            await Task.Run(()=>write(temporary),cancellationToken);cancellationToken.ThrowIfCancellationRequested();File.Move(temporary,path,false);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public Task ExportExcelAsync(ReportSnapshot report,string path,CancellationToken cancellationToken=default)=>AtomicWriteAsync(path,temp=>
    {
        using var workbook=new XLWorkbook();
        void Roster(string name,IEnumerable<DutyRow> data)
        {
            var rows=data.ToArray();var sheet=workbook.Worksheets.Add(name);
            string[] headers=["Date","Examination","Venue","Position","Start","End","Teacher","Minutes"];
            for(int c=0;c<headers.Length;c++)sheet.Cell(1,c+1).Value=headers[c];
            for(int i=0;i<rows.Length;i++)
            {
                cancellationToken.ThrowIfCancellationRequested();var row=rows[i];int r=i+2;
                sheet.Cell(r,1).Value=row.Date.ToDateTime(TimeOnly.MinValue);sheet.Cell(r,1).Style.DateFormat.Format="yyyy-mm-dd";
                sheet.Cell(r,2).Value=row.Examination;sheet.Cell(r,3).Value=row.Venue;sheet.Cell(r,4).Value=row.Position;
                sheet.Cell(r,5).Value=row.Start.ToTimeSpan();sheet.Cell(r,6).Value=row.End.ToTimeSpan();sheet.Range(r,5,r,6).Style.DateFormat.Format="hh:mm";
                sheet.Cell(r,7).Value=row.Teacher;sheet.Cell(r,8).Value=row.Minutes;
            }
            sheet.Cell(rows.Length+2,7).Value="Total minutes";sheet.Cell(rows.Length+2,8).Value=rows.Sum(x=>x.Minutes);Format(sheet,rows.Length+1,8);
        }
        Roster("Full roster",report.Duties);
        int index=0;foreach(var teacher in report.TeacherNames.OrderBy(x=>x.Value))Roster($"Teacher {++index:000}",report.Duties.Where(x=>x.TeacherId==teacher.Key));
        Roster("Venue roster",report.Duties.OrderBy(x=>x.Venue).ThenBy(x=>x.Date).ThenBy(x=>x.Start));
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
