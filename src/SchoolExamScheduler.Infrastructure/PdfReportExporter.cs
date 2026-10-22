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
    public Task ExportPdfAsync(ReportSnapshot report,string path,ReportKind kind,Guid? teacherId=null,CancellationToken cancellationToken=default)=>AtomicWriteAsync(path,temp=>
    {
        _=FontSetup.Value;
        if(kind==ReportKind.TeacherRoster && (teacherId is null || !report.TeacherNames.ContainsKey(teacherId.Value)))throw new ArgumentException("Select a teacher for an individual roster.");
        using var pdf=new PdfDocument();pdf.Info.Title=$"{report.SchoolName} — {report.RosterName}";pdf.Info.Subject=report.Period;
        var font=new XFont("DejaVu Sans",9);var bold=new XFont("DejaVu Sans",9,XFontStyleEx.Bold);var title=new XFont("DejaVu Sans",14,XFontStyleEx.Bold);
        string[] headers;IReadOnlyList<string[]> rows;
        if(kind==ReportKind.Workload)
        {
            headers=["Teacher","Weight","Eligible min","Target min","Actual min","Difference","% target","Duties"];
            rows=report.Fairness.Teachers.Select(x=>new[]{report.TeacherNames[x.TeacherId],x.Weight.ToString("0.##"),x.AvailableMinutes.ToString(),x.TargetMinutes.ToString("F1"),x.ActualMinutes.ToString(),x.Difference.ToString("F1"),x.PercentageOfTarget?.ToString("F1")??"N/A",x.AssignmentCount.ToString()}).ToArray();
        }
        else
        {
            headers=["Date","Examination","Venue","Position","Start","End","Teacher","Minutes"];
            var duties=report.Duties.AsEnumerable();if(kind==ReportKind.TeacherRoster)duties=duties.Where(x=>x.TeacherId==teacherId);if(kind==ReportKind.VenueRoster)duties=duties.OrderBy(x=>x.Venue).ThenBy(x=>x.Date).ThenBy(x=>x.Start);
            rows=duties.Select(x=>new[]{x.Date.ToString("yyyy-MM-dd"),x.Examination,x.Venue,x.Position.ToString(),x.Start.ToString("HH:mm"),x.End.ToString("HH:mm"),x.Teacher,x.Minutes.ToString()}).ToArray();
        }
        XGraphics? graphics=null;double y=0;PdfPage? page=null;
        double[] relative=kind==ReportKind.Workload?[180,50,80,80,80,80,70,60]:[85,165,65,55,55,55,175,65];
        double[] widths=[];
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
        void NewPage()
        {
            graphics?.Dispose();page=pdf.AddPage();page.Size=PageSize.A4;page.Orientation=PageOrientation.Landscape;graphics=XGraphics.FromPdfPage(page);
            var usable=page.Width.Point-56;widths=relative.Select(w=>w/relative.Sum()*usable).ToArray();y=32;
            foreach(var line in Wrap(report.SchoolName,usable,title)){graphics.DrawString(line,title,XBrushes.Black,28,y);y+=18;}
            foreach(var line in Wrap($"{report.RosterName} | {kind} | {report.Status}",usable,bold)){graphics.DrawString(line,bold,XBrushes.Black,28,y);y+=13;}
            graphics.DrawString(report.Period,font,XBrushes.Black,28,y);y+=22;
            double x=28;for(int c=0;c<headers.Length;c++){graphics.DrawString(headers[c],bold,XBrushes.Black,x+3,y);x+=widths[c];}y+=16;
            graphics.DrawLine(XPens.Gray,28,y-10,page.Width.Point-28,y-10);
            graphics.DrawString($"Page {pdf.PageCount}",font,XBrushes.Gray,28,page.Height.Point-20);
        }
        try
        {
            NewPage();
            foreach(var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();var wrapped=row.Select((cell,c)=>Wrap(cell,widths[c]-7,font)).ToArray();var lineCount=wrapped.Max(x=>x.Length);
                for(int line=0;line<lineCount;line++)
                {
                    if(y>page!.Height.Point-42)NewPage();double x=28;
                    for(int c=0;c<row.Length;c++){if(line<wrapped[c].Length)graphics!.DrawString(wrapped[c][line],font,XBrushes.Black,x+3,y);x+=widths[c];}y+=13;
                }
                y+=5;graphics!.DrawLine(XPens.LightGray,28,y-11,page!.Width.Point-28,y-11);
            }
            if(rows.Count==0)graphics!.DrawString("No duties to report.",font,XBrushes.Black,28,y);
            graphics?.Dispose();graphics=null;pdf.Save(temp);
        }
        finally{graphics?.Dispose();}
    },cancellationToken);
}
