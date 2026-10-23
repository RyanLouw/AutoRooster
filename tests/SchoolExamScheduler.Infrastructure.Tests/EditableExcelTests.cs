using ClosedXML.Excel;
using SchoolExamScheduler.Application;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Infrastructure;
using SchoolExamScheduler.Scheduling;
using Xunit;

public sealed class EditableExcelTests
{
    [Fact]
    public async Task ChangingTeacherOrTimesRecalculatesEditableHoursWithoutChangingOriginalReports()
    {
        var first=Guid.Parse("00000000-0000-0000-0000-000000000001");var second=Guid.Parse("00000000-0000-0000-0000-000000000002");
        var report=new ReportSnapshot("School","December","Roster",RosterStatus.Validated,2,2,1,1,120,
            [new(Guid.NewGuid(),first,new(2026,12,1),"Same Name","Grade 10 Maths","Hall",1,new(8,0),new(10,0),false,120)],[],
            new FairnessStatistics([new(first,1,120,60,120,1,1),new(second,1,120,60,0,0,0)]),new Dictionary<Guid,string>{{first,"Same Name"},{second,"Same Name"}});
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
        try
        {
            await new FileReportExporter().ExportExcelAsync(report,path);using var book=new XLWorkbook(path);
            Assert.Contains(book.Worksheets,s=>s.Name=="Start here");
            var edit=book.Worksheet("Editable roster");var hours=book.Worksheet("Editable hours");var list=book.Worksheet("Teacher list");
            Assert.NotEqual(list.Cell(2,1).GetString(),list.Cell(3,1).GetString());
            Assert.True(edit.Cell(2,7).HasDataValidation);Assert.True(edit.Cell(2,8).HasFormula);
            Assert.Equal(2,hours.Cell(2,4).GetDouble(),5);Assert.Equal(0,hours.Cell(3,4).GetDouble(),5);
            edit.Cell(2,7).Value=list.Cell(3,1).Value;edit.Cell(2,6).Value=new TimeSpan(9,30,0);
            book.RecalculateAllFormulas();
            Assert.Equal(0,hours.Cell(2,4).GetDouble(),5);Assert.Equal(1.5,hours.Cell(3,4).GetDouble(),5);
            Assert.Equal(0.5,hours.Cell(3,5).GetDouble(),5);
            Assert.Equal("Same Name",book.Worksheet("Full roster").Cell(2,7).GetString());
            Assert.Equal(120,book.Worksheet("Full roster").Cell(2,8).GetValue<int>());
        }
        finally{if(File.Exists(path))File.Delete(path);}
    }
    [Fact]
    public async Task WorkbookFlagsConflictingManualChangesAndHandlesEmptyRosters()
    {
        var teacher=Guid.NewGuid();var other=Guid.NewGuid();var row=new DutyRow(Guid.NewGuid(),teacher,new(2026,12,1),"Ada Zulu","Maths","Hall",1,new(8,0),new(10,0),false,120);
        var report=new ReportSnapshot("School","December","Test",RosterStatus.Validated,2,2,1,1,240,[row,row with{Id=Guid.NewGuid(),TeacherId=other,Teacher="Bea Zulu",Position=2}],[],new([]),new Dictionary<Guid,string>{{teacher,"Ada Zulu"},{other,"Bea Zulu"}}){MinimumBreakMinutes=15};
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
        try
        {
            await new FileReportExporter().ExportExcelAsync(report,path);using(var book=new XLWorkbook(path))
            {
                var edit=book.Worksheet("Editable roster");Assert.Equal("",edit.Cell(2,10).GetString());
                edit.Cell(3,7).Value=edit.Cell(2,7).Value;book.RecalculateAllFormulas();Assert.Equal("OVERLAP / SHORT BREAK",edit.Cell(2,10).GetString());
                edit.Cell(2,6).Value=new TimeSpan(7,30,0);book.RecalculateAllFormulas();Assert.Equal("CHECK TIMES",edit.Cell(2,10).GetString());
            }
            File.Delete(path);report=report with{Duties=[],TeacherNames=new Dictionary<Guid,string>()};
            await new FileReportExporter().ExportExcelAsync(report,path);using var empty=new XLWorkbook(path);
            empty.RecalculateAllFormulas();Assert.Equal(0,empty.Worksheet("Editable hours").Cell(2,4).GetDouble());
        }
        finally{if(File.Exists(path))File.Delete(path);}
    }

}
