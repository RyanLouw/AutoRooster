using System.Globalization;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using SchoolExamScheduler.Application;
namespace SchoolExamScheduler.Infrastructure;
public sealed partial class FileReportExporter
{
    private static readonly Lazy<bool> FontSetup=new(()=>{GlobalFontSettings.FontResolver=new EmbeddedFonts();return true;});
    private sealed class EmbeddedFonts : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName,bool bold,bool italic)=>new(bold?"DejaVuSans-Bold":"DejaVuSans");
        public byte[] GetFont(string faceName)
        {
            using var stream=typeof(FileReportExporter).Assembly.GetManifestResourceStream($"SchoolExamScheduler.Infrastructure.Fonts.{faceName}.ttf")??throw new InvalidOperationException("Embedded report font is missing.");
            using var buffer=new MemoryStream();stream.CopyTo(buffer);return buffer.ToArray();
        }
    }
    private sealed record PdfSection(string Title,string[] Headers,IReadOnlyList<string[]> Rows,double[] Widths,string Summary,string EmptyMessage="No duties assigned.");
    private static string Number(double number)=>number.ToString("0.00",CultureInfo.InvariantCulture);
    private static string Total(int minutes)=>$"{Number(minutes/60.0)} hours ({ReportTime.Duration(minutes)})";
    private static IReadOnlyList<PdfSection> Sections(ReportSnapshot report,ReportKind kind,Guid? teacherId)
    {
        if(kind==ReportKind.AllReports)
            return new[]{ReportKind.Workload,ReportKind.TeacherTimetables,ReportKind.FullRoster,ReportKind.VenueRoster,ReportKind.Calendar,ReportKind.UnfilledDuties}.SelectMany(k=>Sections(report,k,null)).ToArray();
        if(kind==ReportKind.Workload)
            return [new("Teacher hours",["Teacher","Total hours","Total hh:mm","Target hours","Difference hours","Duties","Days"],
                report.TeacherHours.Select(t=>new[]{t.Teacher,Number(t.Hours),t.Duration,Number(t.TargetHours),Number(t.DifferenceHours),t.Assignments.ToString(CultureInfo.InvariantCulture),t.Days.ToString(CultureInfo.InvariantCulture)}).ToArray(),
                [230,90,90,95,105,70,60],$"Total supervision: {Total(report.AssignedMinutes)}. Hours include all teachers and all dates in this roster.","No teachers to report."),
                new("Workload fairness",["Teacher","Weight","Eligible hours","Target hours","Actual hours","Difference hours","% target","Duties"],
                report.Fairness.Teachers.Select(t=>new[]{report.TeacherNames[t.TeacherId],t.Weight.ToString("0.##",CultureInfo.InvariantCulture),Number(t.AvailableMinutes/60.0),Number(t.TargetMinutes/60.0),Number(t.ActualMinutes/60.0),Number(t.Difference/60.0),t.PercentageOfTarget is double percent?Number(percent):"N/A",t.AssignmentCount.ToString(CultureInfo.InvariantCulture)}).ToArray(),
                [190,65,95,95,95,105,70,65],"Targets account for workload weights and availability. Hard constraints can prevent an exact match; N/A indicates a zero target.","No eligible or previously assigned teachers.")];
        if(kind==ReportKind.UnfilledDuties)
            return [new("Unfilled duties",["Date","Examination","Venue","Position","Start","End","Missing hh:mm","Missing hours"],
                report.Unfilled.Select(x=>new[]{x.Date.ToString("yyyy-MM-dd"),x.Examination,x.Venue,x.Position.ToString(CultureInfo.InvariantCulture),x.Start.ToString("HH:mm"),x.End.ToString("HH:mm"),ReportTime.Duration(x.Minutes),Number(x.Minutes/60.0)}).ToArray(),
                [85,190,75,55,55,55,100,90],$"Missing supervision: {Total(report.Unfilled.Sum(x=>x.Minutes))}.","No uncovered duties.")];
        if(kind is ReportKind.TeacherRoster or ReportKind.TeacherTimetables)
        {
            if(kind==ReportKind.TeacherRoster && (teacherId is null || !report.TeacherNames.ContainsKey(teacherId.Value)))throw new ArgumentException("Select a teacher for an individual roster.");
            return report.TeacherHours.Where(t=>kind==ReportKind.TeacherTimetables || t.TeacherId==teacherId).Select(t=>RosterSection($"Teacher timetable — {t.Teacher} — Total: {Total(t.Minutes)}",report.Duties.Where(d=>d.TeacherId==t.TeacherId).OrderBy(d=>d.Date).ThenBy(d=>d.Start))).ToArray();
        }
        if(kind is not(ReportKind.FullRoster or ReportKind.VenueRoster or ReportKind.Calendar))throw new ArgumentOutOfRangeException(nameof(kind));
        var rows=kind==ReportKind.VenueRoster?report.Duties.OrderBy(d=>d.Venue).ThenBy(d=>d.Date).ThenBy(d=>d.Start).ThenBy(d=>d.Position):report.Duties.OrderBy(d=>d.Date).ThenBy(d=>d.Start).ThenBy(d=>d.Examination).ThenBy(d=>d.Venue).ThenBy(d=>d.Position);
        return [RosterSection(kind switch{ReportKind.VenueRoster=>"Venue roster",ReportKind.Calendar=>"Calendar / examination timeline",_=>"Full examination roster"},rows)];
    }
    private static PdfSection RosterSection(string title,IEnumerable<DutyRow> duties)
    {
        var rows=duties.ToArray();return new(title,["Date","Examination","Venue","Position","Start","End","Teacher","Duration","Hours","Session"],
            rows.Select(x=>new[]{x.Date.ToString("yyyy-MM-dd"),x.Examination,x.Venue,x.Position.ToString(CultureInfo.InvariantCulture),x.Start.ToString("HH:mm"),x.End.ToString("HH:mm"),x.Teacher,x.Duration,Number(x.Hours),x.Session}).ToArray(),
            [85,160,60,55,50,50,155,65,55,75],$"Total supervision: {Total(rows.Sum(x=>x.Minutes))}. Each position is an invigilator needed simultaneously; Split rows show handovers within that position.");
    }
    public Task ExportPdfAsync(ReportSnapshot report,string path,ReportKind kind,Guid? teacherId=null,CancellationToken cancellationToken=default)=>AtomicWriteAsync(path,temp=>
    {
        _=FontSetup.Value;var sections=Sections(report,kind,teacherId);
        using var pdf=new PdfDocument();pdf.Info.Title=$"{report.SchoolName} — {report.RosterName}";pdf.Info.Subject=report.Period;
        var font=new XFont("DejaVu Sans",9);var bold=new XFont("DejaVu Sans",9,XFontStyleEx.Bold);var title=new XFont("DejaVu Sans",14,XFontStyleEx.Bold);
        XGraphics? graphics=null;double y=0;PdfPage? page=null;
        string[] Wrap(string text,double width,XFont selectedFont)
        {
            var lines=new List<string>();var line="";
            foreach(var ch in text.Replace('\r',' '))
            {
                if(ch=='\n'){lines.Add(line);line="";continue;}
                if(line.Length>0 && graphics!.MeasureString(line+ch,selectedFont).Width>width){lines.Add(line);line="";}
                line+=ch;
            }
            lines.Add(line);return lines.ToArray();
        }
        void Render(PdfSection section)
        {
            double[] widths=[];
            void NewPage()
            {
                cancellationToken.ThrowIfCancellationRequested();
                graphics?.Dispose();page=pdf.AddPage();page.Size=PageSize.A4;page.Orientation=PageOrientation.Landscape;graphics=XGraphics.FromPdfPage(page);
                var usable=page.Width.Point-56;widths=section.Widths.Select(w=>w/section.Widths.Sum()*usable).ToArray();y=32;
                foreach(var line in Wrap(report.SchoolName,usable,title)){graphics.DrawString(line,title,XBrushes.Black,28,y);y+=18;}
                foreach(var line in Wrap($"{report.RosterName} | {report.Status} | {section.Title}",usable,bold)){graphics.DrawString(line,bold,XBrushes.Black,28,y);y+=13;}
                graphics.DrawString(report.Period,font,XBrushes.Black,28,y);y+=18;
                var headings=section.Headers.Select((h,c)=>Wrap(h,widths[c]-7,bold)).ToArray();var headingLines=headings.Max(h=>h.Length);
                for(int line=0;line<headingLines;line++)
                {
                    double x=28;for(int c=0;c<headings.Length;c++){if(line<headings[c].Length)graphics.DrawString(headings[c][line],bold,XBrushes.Black,x+3,y);x+=widths[c];}y+=12;
                }
                y+=6;graphics.DrawLine(XPens.Gray,28,y-10,page.Width.Point-28,y-10);
                graphics.DrawString($"Page {pdf.PageCount}",font,XBrushes.Gray,28,page.Height.Point-20);
            }
            NewPage();
            foreach(var row in section.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();var wrapped=row.Select((cell,c)=>Wrap(cell,widths[c]-7,font)).ToArray();var lineCount=wrapped.Max(x=>x.Length);
                for(int line=0;line<lineCount;line++)
                {
                    if(y>page!.Height.Point-42)NewPage();double x=28;
                    for(int c=0;c<row.Length;c++){if(line<wrapped[c].Length)graphics!.DrawString(wrapped[c][line],font,XBrushes.Black,x+3,y);x+=widths[c];}y+=13;
                }
                y+=5;graphics!.DrawLine(XPens.LightGray,28,y-11,page!.Width.Point-28,y-11);
            }
            if(section.Rows.Count==0){graphics!.DrawString(section.EmptyMessage,font,XBrushes.Black,28,y);y+=20;}
            foreach(var line in Wrap(section.Summary,page!.Width.Point-56,font))
            {if(y>page.Height.Point-42)NewPage();graphics!.DrawString(line,font,XBrushes.Black,28,y);y+=13;}
        }
        try
        {
            foreach(var section in sections)Render(section);
            if(pdf.PageCount==0)Render(new("Teacher timetables",["Teacher"],[],[1],"No teachers to report."));
            graphics?.Dispose();graphics=null;pdf.Save(temp);
        }
        finally{graphics?.Dispose();}
    },cancellationToken);
}
