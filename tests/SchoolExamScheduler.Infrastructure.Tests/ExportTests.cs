using ClosedXML.Excel;
using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Infrastructure;
using SchoolExamScheduler.Scheduling;
using Xunit;
public sealed class ExportTests
{
    private static ReportSnapshot Report()
    {
        var teacher=Guid.NewGuid();return new("Test School","10 Dec 2026","December",RosterStatus.Validated,1,1,1,1,120,[new(Guid.NewGuid(),teacher,new(2026,12,10),"Ada Zulu","Grade 8 Mathematics","A1",1,new(8,0),new(10,0),false,120)],[],new([new(teacher,1,120,120,120,1,1)]),new Dictionary<Guid,string>{{teacher,"Ada Zulu"}});
    }
    [Fact]
    public async Task WorkbookContainsTypedDatesTimesTotalsAndAllReportSheets()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
        try
        {
            var report=Report();await new FileReportExporter().ExportExcelAsync(report,path);
            using var workbook=new XLWorkbook(path);
            Assert.Contains(workbook.Worksheets,s=>s.Name=="Full roster");Assert.Contains(workbook.Worksheets,s=>s.Name=="Workload");Assert.Contains(workbook.Worksheets,s=>s.Name=="Unfilled duties");Assert.Contains(workbook.Worksheets,s=>s.Name=="Venue roster");Assert.Contains(workbook.Worksheets,s=>s.Name.StartsWith("Teacher "));
            var sheet=workbook.Worksheet("Full roster");Assert.Equal(new DateTime(2026,12,10),sheet.Cell(2,1).GetDateTime());Assert.Equal(new TimeSpan(8,0,0),sheet.Cell(2,5).GetTimeSpan());Assert.Equal("Ada Zulu",sheet.Cell(2,7).GetString());Assert.Equal(120,sheet.Cell(2,8).GetValue<int>());Assert.Equal(120,sheet.Cell(3,8).GetValue<int>());
        }
        finally{if(File.Exists(path))File.Delete(path);}
    }
    [Theory]
    [InlineData(ReportKind.FullRoster)]
    [InlineData(ReportKind.TeacherRoster)]
    [InlineData(ReportKind.Workload)]
    [InlineData(ReportKind.VenueRoster)]
    public async Task PdfIsStructurallyValidAndContainsExpectedText(ReportKind kind)
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".pdf");
        try
        {
            var report=Report();await new FileReportExporter().ExportPdfAsync(report,path,kind,report.TeacherNames.Keys.Single());
            using var pdf=UglyToad.PdfPig.PdfDocument.Open(path);Assert.True(pdf.NumberOfPages>=1);
            var text=string.Join(" ",pdf.GetPages().Select(x=>x.Text));Assert.Contains("Test School",text);Assert.Contains("Ada Zulu",text);Assert.Contains("December",text);
        }
        finally{if(File.Exists(path))File.Delete(path);}
    }
    [Fact]
    public async Task ExistingOutputIsNeverOverwrittenImplicitly()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");await File.WriteAllTextAsync(path,"existing");
        try{await Assert.ThrowsAsync<IOException>(()=>new FileReportExporter().ExportExcelAsync(Report(),path));Assert.Equal("existing",await File.ReadAllTextAsync(path));}
        finally{File.Delete(path);}
    }
    [Fact]
    public async Task LongPdfReportsRepeatHeadersAndKeepTextInsidePages()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".pdf");
        try
        {
            var report=Report();var row=report.Duties.Single();report=report with {Duties=Enumerable.Range(0,150).Select(i=>row with{Teacher="Teacher "+i+" "+new string('Z',180)}).ToArray()};
            await new FileReportExporter().ExportPdfAsync(report,path,ReportKind.FullRoster);
            using var pdf=UglyToad.PdfPig.PdfDocument.Open(path);Assert.True(pdf.NumberOfPages>1);
            foreach(var page in pdf.GetPages())
            {
                Assert.Contains("Examination",page.Text);Assert.Contains("Test School",page.Text);
                Assert.All(page.Letters,l=>{Assert.True(l.BoundingBox.Left>=0);Assert.True(l.BoundingBox.Right<=page.Width);Assert.True(l.BoundingBox.Bottom>=0);Assert.True(l.BoundingBox.Top<=page.Height);});
            }
        }
        finally{if(File.Exists(path))File.Delete(path);}
    }

}
