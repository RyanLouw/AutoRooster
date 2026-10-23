using System.Text.Json;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
namespace SchoolExamScheduler.Application;

public sealed record ArchivedRosterSnapshot(ReportSnapshot Report,IReadOnlyList<SchedulingIssue> ValidationIssues)
{
    public static ArchivedRosterSnapshot Read(RosterVersion roster)=>JsonSerializer.Deserialize<ArchivedRosterSnapshot>(roster.ArchivedSnapshot)
        ??throw new InvalidDataException("The archived roster snapshot could not be read.");
}
public sealed record ExamSeriesRollover(int ExamsArchived,int RostersArchived);
public sealed class ExamSeriesService(ISchoolDataStore store,ReportingService reports,ManualRosterService manual)
{
    public async Task<ExamSeriesRollover> FinishAsync(IReadOnlyCollection<Guid>? teachersToKeep=null,CancellationToken cancellationToken=default)
    {
        var exams=(await store.ListAsync<Exam>(cancellationToken)).Where(e=>e.IsActive).ToArray();
        if(exams.Length==0)throw new InvalidOperationException("There are no active exams to finish. Add the next series' exams in Examinations.");
        var teachers=await store.ListAsync<Teacher>(cancellationToken);
        if(teachersToKeep is not null && teachersToKeep.Any(id=>!teachers.Any(t=>t.Id==id)))throw new ArgumentException("A selected teacher no longer exists. Reload Settings and choose the staff to keep.");
        var rosters=(await store.ListAsync<RosterVersion>(cancellationToken)).Where(r=>!r.IsArchived).ToArray();
        var snapshots=new Dictionary<Guid,string>();
        // Build every historical snapshot before changing any active exams or rosters.
        foreach(var roster in rosters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            snapshots.Add(roster.Id,JsonSerializer.Serialize(new ArchivedRosterSnapshot(await reports.BuildAsync(roster.Id,cancellationToken),await manual.ValidateAsync(roster.Id))));
        }
        cancellationToken.ThrowIfCancellationRequested();
        foreach(var roster in rosters)roster.Archive(snapshots[roster.Id]);
        foreach(var exam in exams)exam.IsActive=false;
        var removed=teachersToKeep is null ? [] : teachers.Where(t=>t.IsActive && !teachersToKeep.Contains(t.Id)).ToArray();
        foreach(var teacher in removed)teacher.IsActive=false;
        await store.CommitAsync([..rosters,..exams,..removed,new AuditRecord("Finish exam series",$"Archived {exams.Length} exams and {rosters.Length} roster versions; {removed.Length} teachers removed from the active staff list; subjects, venues and dated availability retained.")],[],cancellationToken);
        return new(exams.Length,rosters.Length);
    }
}
