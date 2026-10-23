using ClosedXML.Excel;
using SchoolExamScheduler.Application;
namespace SchoolExamScheduler.Infrastructure;

public sealed partial class FileReportExporter
{
    private static void AddEditableSheets(XLWorkbook book,ReportSnapshot report,CancellationToken token)
    {
        book.CalculateMode=XLCalculateMode.Auto;
        var instructions=book.Worksheets.Add("Start here");
        string[] guidance=
        [
            $"{report.SchoolName} — {report.RosterName} — {report.Period}",
            "FINAL MANUAL ADJUSTMENTS — this workbook does not change or import into the application.",
            "1. Open Editable roster. Use the blue Teacher dropdown to reassign an existing duty; each choice includes an employee number or unique ID.",
            "2. Editable hours recalculates automatically from Editable roster. Target hours remain the targets calculated by the application at export.",
            "3. To swap two teachers, change the Teacher dropdown on both duty rows. Keep the date, room, position and times unless a handover must change.",
            "4. Blue Start/End cells may be changed using 24-hour times. Check continuity of every room/position yourself; a time change can leave a coverage gap.",
            $"5. Check messages flag invalid times, unknown teachers and overlaps or breaks shorter than {report.MinimumBreakMinutes} minutes. They do not check teacher leave, limits, locks or complete coverage.",
            "6. The original Full roster, Teacher hours and other report sheets are snapshots for comparison. Only Editable hours follows your manual changes.",
            "7. To change teacher leave, responsibilities or targets: change Teacher Availability / workload weight in the app, save, generate a new roster and export again.",
            "8. In Excel, leave calculation set to Automatic. Blue cells are inputs; green cells are formulas. Use filters and save a copy of your adjusted workbook.",
            "9. Edit existing rows only. Adding/deleting rows or overwriting formulas can invalidate the totals. Review and approve the final workbook before using it.",
            $"Required supervision: {report.RequiredHours:F2} hours. Equal-share reference: {(report.EligibleTeachers>0?report.RequiredHours/report.EligibleTeachers:0):F2} hours per eligible teacher; weighted targets account for availability and workload responsibilities."
        ];
        for(int i=0;i<guidance.Length;i++)instructions.Cell(i+1,1).Value=guidance[i];
        instructions.Column(1).Width=125;instructions.Column(1).Style.Alignment.WrapText=true;instructions.Rows(1,guidance.Length).Height=44;
        instructions.Cell(1,1).Style.Font.Bold=true;instructions.Cell(2,1).Style.Font.Bold=true;
        var edit=book.Worksheets.Add("Editable roster");var summary=book.Worksheets.Add("Editable hours");var list=book.Worksheets.Add("Teacher list");
        var teachers=report.TeacherHours.ToArray();var labels=new Dictionary<Guid,string>();
        for(int i=0;i<teachers.Length;i++)
        {
            var teacher=teachers[i];var code=report.TeacherCodes.GetValueOrDefault(teacher.TeacherId,teacher.TeacherId.ToString("N"));
            var label=$"{teacher.Teacher} [{code}]";
            if(labels.Values.Contains(label))label+=$" ({teacher.TeacherId:N})";
            labels.Add(teacher.TeacherId,label);
            list.Cell(i+2,1).Value=label;list.Cell(i+2,2).Value=code;list.Cell(i+2,3).Value=teacher.Teacher;
            list.Cell(i+2,4).Value=teacher.TargetHours;
        }
        string[] listHeaders=["Teacher choice","Employee number / ID","Teacher","Target hours"];
        for(int i=0;i<listHeaders.Length;i++)list.Cell(1,i+1).Value=listHeaders[i];
        Format(list,teachers.Length+1,4);
        if(teachers.Length>0)list.Range(2,1,teachers.Length+1,1).AddToNamed("TeacherChoices");
        string[] headers=["Date","Examination","Venue","Position","Start (edit)","End (edit)","Teacher (edit)","Minutes (formula)","Hours (formula)","Check (formula)","Originally locked"];
        for(int i=0;i<headers.Length;i++)edit.Cell(1,i+1).Value=headers[i];
        int last=Math.Max(2,report.Duties.Count+1),teacherLast=Math.Max(2,teachers.Length+1);
        for(int i=0;i<report.Duties.Count;i++)
        {
            token.ThrowIfCancellationRequested();var duty=report.Duties[i];int r=i+2;
            edit.Cell(r,1).Value=duty.Date.ToDateTime(TimeOnly.MinValue);edit.Cell(r,1).Style.DateFormat.Format="yyyy-mm-dd";
            edit.Cell(r,2).Value=duty.Examination;edit.Cell(r,3).Value=duty.Venue;edit.Cell(r,4).Value=duty.Position;
            edit.Cell(r,5).Value=duty.Start.ToTimeSpan();edit.Cell(r,6).Value=duty.End.ToTimeSpan();edit.Range(r,5,r,6).Style.DateFormat.Format="hh:mm";
            edit.Cell(r,7).Value=labels[duty.TeacherId];edit.Range(r,5,r,7).Style.Fill.BackgroundColor=XLColor.FromHtml("#DDEEFF");
            if(teachers.Length>0)
            {
                var validation=edit.Cell(r,7).CreateDataValidation();validation.List("=TeacherChoices",true);
                validation.IgnoreBlanks=false;validation.ShowErrorMessage=true;validation.ErrorTitle="Choose a listed teacher";validation.ErrorMessage="Select a teacher from the dropdown.";
            }
            edit.Cell(r,8).FormulaA1=$"IF(OR(E{r}=\"\",F{r}=\"\",F{r}<=E{r}),0,ROUND((F{r}-E{r})*1440,0))";
            edit.Cell(r,9).FormulaA1=$"H{r}/60";
            edit.Cell(r,10).FormulaA1=$"IF(OR(E{r}=\"\",F{r}=\"\",E{r}<0,F{r}>=1,F{r}<=E{r}),\"CHECK TIMES\",IF(COUNTIF('Teacher list'!$A$2:$A${teacherLast},G{r})<>1,\"SELECT TEACHER\",IF(COUNTIFS($A$2:$A${last},A{r},$G$2:$G${last},G{r},$E$2:$E${last},\"<\"&(F{r}+{report.MinimumBreakMinutes}/1440),$F$2:$F${last},\">\"&(E{r}-{report.MinimumBreakMinutes}/1440))>1,\"OVERLAP / SHORT BREAK\",\"\")))";
            edit.Cell(r,11).Value=duty.IsLocked?"Yes":"No";
            edit.Range(r,8,r,10).Style.Fill.BackgroundColor=XLColor.FromHtml("#E5F2E5");
        }
        edit.Column(9).Style.NumberFormat.Format="0.00";Format(edit,report.Duties.Count+1,11);edit.SheetView.FreezeColumns(4);
        var dates=report.Dates;string[] summaryHeaders=["Teacher choice","Employee number / ID","Target hours (export)","Adjusted hours (formula)","Difference hours (formula)","Adjusted duration (formula)"];
        for(int i=0;i<summaryHeaders.Length;i++)summary.Cell(1,i+1).Value=summaryHeaders[i];
        for(int d=0;d<dates.Count;d++){summary.Cell(1,d+7).Value=dates[d].ToDateTime(TimeOnly.MinValue);summary.Cell(1,d+7).Style.DateFormat.Format="yyyy-mm-dd";}
        for(int i=0;i<teachers.Length;i++)
        {
            token.ThrowIfCancellationRequested();int r=i+2;var teacher=teachers[i];
            summary.Cell(r,1).Value=labels[teacher.TeacherId];summary.Cell(r,2).Value=list.Cell(r,2).Value;summary.Cell(r,3).Value=teacher.TargetHours;
            summary.Cell(r,4).FormulaA1=$"SUMIF('Editable roster'!$G$2:$G${last},A{r},'Editable roster'!$I$2:$I${last})";
            summary.Cell(r,5).FormulaA1=$"D{r}-C{r}";summary.Cell(r,6).FormulaA1=$"D{r}/24";summary.Cell(r,6).Style.NumberFormat.Format="[h]:mm";
            for(int d=0;d<dates.Count;d++)summary.Cell(r,d+7).FormulaA1=$"SUMIFS('Editable roster'!$I$2:$I${last},'Editable roster'!$G$2:$G${last},$A{r},'Editable roster'!$A$2:$A${last},{XLHelper.GetColumnLetterFromNumber(d+7)}$1)";
        }
        int total=teachers.Length+2;summary.Cell(total,1).Value="Total";summary.Cell(total,3).Value=teachers.Sum(t=>t.TargetHours);
        if(teachers.Length>0)summary.Cell(total,4).FormulaA1=$"SUM(D2:D{total-1})";else summary.Cell(total,4).Value=0;summary.Cell(total,6).FormulaA1=$"D{total}/24";summary.Cell(total,6).Style.NumberFormat.Format="[h]:mm";
        summary.Columns(3,5).Style.NumberFormat.Format="0.00";if(dates.Count>0)summary.Columns(7,dates.Count+6).Style.NumberFormat.Format="0.00";
        if(teachers.Length>0)summary.Range(2,4,teachers.Length+1,dates.Count+6).Style.Fill.BackgroundColor=XLColor.FromHtml("#E5F2E5");
        Format(summary,total-1,dates.Count+6);summary.SheetView.FreezeColumns(2);
    }
}
