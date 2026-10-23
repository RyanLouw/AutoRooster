using System.IO.Compression;
using ClosedXML.Excel;
using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Infrastructure;
using SchoolExamScheduler.Scheduling;
using Xunit;

public sealed class AllReportsExportTests : IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"all-reports-"+Guid.NewGuid());
    public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private static ReportSnapshot SplitReport()
    {
        var a=Guid.NewGuid();var b=Guid.NewGuid();var c=Guid.NewGuid();var allocation=Guid.NewGuid();var date=new DateOnly(2026,12,10);
        return new("Test School","10 Dec 2026","Split roster",RosterStatus.Validated,3,3,1,1,60,
            [new(Guid.NewGuid(),a,date,"Ada Zulu","Grade 8 Mathematics","A1",1,new(8,0),new(8,30),false,30,allocation,new(8,0),new(9,0)),
             new(Guid.NewGuid(),b,date,"Bea Zee","Grade 8 Mathematics","A1",1,new(8,30),new(9,0),false,30,allocation,new(8,0),new(9,0))],[],
            new([new(a,1,60,20,30,1,1),new(b,1,60,20,30,1,1),new(c,1,60,20,0,0,0)]),
            new Dictionary<Guid,string>{{a,"Ada Zulu"},{b,"Bea Zee"},{c,"Chris No Duties"}});
    }
    [Fact]
    public async Task WorkbooksShowEveryTeachersHoursDailyTimelineAndSplitSegments()
    {
        Directory.CreateDirectory(directory);var path=Path.Combine(directory,"reports.xlsx");
        await new FileReportExporter().ExportExcelAsync(SplitReport(),path);
        using var workbook=new XLWorkbook(path);var hours=workbook.Worksheet("Teacher hours");
        Assert.Equal("Ada Zulu",hours.Cell(2,1).GetString());Assert.Equal(0.5,hours.Cell(2,2).GetDouble());Assert.Equal("00:30",hours.Cell(2,3).GetString());
        Assert.Equal(0.5,hours.Cell(2,4).GetDouble());Assert.Equal(0,hours.Cell(4,2).GetDouble());Assert.Equal(1,hours.Cell(5,2).GetDouble());
        var timeline=workbook.Worksheet("Teacher timeline");Assert.Equal(new TimeSpan(8,0,0),timeline.Cell(2,5).GetTimeSpan());
        Assert.Equal(new TimeSpan(8,30,0),timeline.Cell(3,5).GetTimeSpan());Assert.Equal("Split",timeline.Cell(2,9).GetString());
    }
    [Fact]
    public async Task PdfBookHasHoursSummaryAndEveryTeachersTimedDutiesIncludingZeroHours()
    {
        Directory.CreateDirectory(directory);var path=Path.Combine(directory,"all.pdf");
        await new FileReportExporter().ExportPdfAsync(SplitReport(),path,ReportKind.AllReports);
        using var pdf=UglyToad.PdfPig.PdfDocument.Open(path);var text=string.Join(" ",pdf.GetPages().Select(p=>p.Text));
        Assert.Contains("Workload fairness",text);Assert.Contains("Weight",text);Assert.Contains("Teacher hours",text);Assert.Contains("00:30",text);Assert.Contains("0.50",text);
        Assert.Contains("Ada Zulu",text);Assert.Contains("Bea Zee",text);Assert.Contains("Chris No Duties",text);
        Assert.Contains("08:00",text);Assert.Contains("08:30",text);Assert.Contains("09:00",text);Assert.Contains("Split",text);
        Assert.True(pdf.NumberOfPages>=6);
    }
    [Fact]
    public async Task ExportAllZipContainsEveryReportAndIndividualTeacherRosters()
    {
        Directory.CreateDirectory(directory);var path=Path.Combine(directory,"all.zip");await new FileReportExporter().ExportAllAsync(SplitReport(),path);
        using var zip=ZipFile.OpenRead(path);var names=zip.Entries.Select(e=>e.FullName).ToArray();
        foreach(var name in new[]{"Reports.xlsx","All-reports.pdf","Teacher-hours.pdf","Teacher-timetables.pdf","Full-roster.pdf","Venue-roster.pdf","Calendar.pdf","Unfilled-duties.pdf"})Assert.Contains(name,names);
        Assert.Equal(3,names.Count(n=>n.StartsWith("Teachers/",StringComparison.Ordinal)));
        Assert.All(zip.Entries,e=>Assert.True(e.Length>0));
    }
    [Fact]
    public async Task BundleCannotOverwriteAnExistingFileAndCancellationLeavesNoPartialBundle()
    {
        Directory.CreateDirectory(directory);var path=Path.Combine(directory,"existing.zip");await File.WriteAllTextAsync(path,"existing");
        await Assert.ThrowsAsync<IOException>(()=>new FileReportExporter().ExportAllAsync(SplitReport(),path));Assert.Equal("existing",await File.ReadAllTextAsync(path));
        var cancelled=Path.Combine(directory,"cancelled.zip");using var cts=new CancellationTokenSource();cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new FileReportExporter().ExportAllAsync(SplitReport(),cancelled,cts.Token));Assert.False(File.Exists(cancelled));
    }
    [Fact]
    public async Task SimultaneousPositionsCountTeacherHoursWithoutDoubleCountingHandoverBoundaries()
    {
        var report=SplitReport();var third=report.TeacherNames.Single(x=>x.Value=="Chris No Duties").Key;var fourth=Guid.NewGuid();
        var names=report.TeacherNames.ToDictionary(x=>x.Key,x=>x.Value);names[fourth]="Dan Four";
        report=report with{RequiredMinutes=120,TotalTeachers=4,EligibleTeachers=4,TeacherNames=names,Duties=[..report.Duties,
            report.Duties[0] with{Id=Guid.NewGuid(),TeacherId=third,Teacher=names[third],Position=2},
            report.Duties[1] with{Id=Guid.NewGuid(),TeacherId=fourth,Teacher=names[fourth],Position=2}]};
        Assert.Equal(2,report.AssignedHours);Assert.Equal(2,report.TeacherHours.Sum(t=>t.Hours));Assert.All(report.TeacherHours,t=>Assert.Equal(0.5,t.Hours));
        Directory.CreateDirectory(directory);var path=Path.Combine(directory,"two-positions.xlsx");await new FileReportExporter().ExportExcelAsync(report,path);
        using var workbook=new XLWorkbook(path);Assert.Equal(2,workbook.Worksheet("Teacher hours").Cell(6,2).GetDouble());Assert.Equal(2,workbook.Worksheet("Teacher hours").Cell(6,4).GetDouble());
    }

}
