using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
namespace SchoolExamScheduler.Application;
public sealed record UnfilledDuty(DateOnly Date,string Examination,string Venue,int Position,TimeOnly Start,TimeOnly End)
{public int Minutes=>(int)(End.ToTimeSpan()-Start.ToTimeSpan()).TotalMinutes;}
public sealed record ReportSnapshot(string SchoolName,string Period,string RosterName,RosterStatus Status,int TotalTeachers,int EligibleTeachers,int TotalExams,int TotalVenues,long RequiredMinutes,IReadOnlyList<DutyRow> Duties,IReadOnlyList<UnfilledDuty> Unfilled,FairnessStatistics Fairness,IReadOnlyDictionary<Guid,string> TeacherNames)
{
    public IReadOnlyDictionary<Guid,string> TeacherCodes {get;init;}=new Dictionary<Guid,string>();
    public int MinimumBreakMinutes {get;init;}
    public int AssignedMinutes=>Duties.Sum(x=>x.Minutes);
    public IReadOnlyList<DateOnly> Dates=>Duties.Select(x=>x.Date).Concat(Unfilled.Select(x=>x.Date)).Distinct().Order().ToArray();
    public IReadOnlyList<TeacherHoursRow> TeacherHours=>TeacherNames.OrderBy(x=>x.Value,StringComparer.CurrentCulture).ThenBy(x=>x.Key).Select(t=>
    {
        var duties=Duties.Where(d=>d.TeacherId==t.Key).ToArray();
        return new TeacherHoursRow(t.Key,t.Value,duties.Sum(d=>d.Minutes),Fairness.Teachers.FirstOrDefault(w=>w.TeacherId==t.Key)?.TargetMinutes??0,duties.Length,duties.Select(d=>d.Date).Distinct().Count());
    }).ToArray();
    public double RequiredHours=>RequiredMinutes/60.0;
    public double AssignedHours=>AssignedMinutes/60.0;
}
public sealed class ReportingService(ISchoolDataStore store,RosterWorkflow workflow)
{
    public async Task<ReportSnapshot> BuildAsync(Guid rosterId,CancellationToken cancellationToken=default)
    {
        var roster=(await store.ListAsync<RosterVersion>(cancellationToken)).Single(x=>x.Id==rosterId);
        var request=await workflow.LoadRequestAsync(rosterId,cancellationToken);
        var assignments=(await store.ListAsync<InvigilationAssignment>(cancellationToken)).Where(x=>x.RosterVersionId==rosterId && x.AssignmentStatus==AssignmentStatus.Assigned).ToArray();
        var teachers=request.Teachers.ToDictionary(x=>x.Id);var subjects=request.Subjects.ToDictionary(x=>x.Id);var venues=request.Venues.ToDictionary(x=>x.Id);var exams=request.Exams.ToDictionary(x=>x.Id);var allocations=request.ExamVenues.ToDictionary(x=>x.Id);
        string Name(Exam exam)=>$"Grade {exam.Grade} {subjects[exam.SubjectId].Name}";
        var rows=assignments.Select(a=>
        {
            var allocation=allocations[a.ExamVenueId];var exam=exams[allocation.ExamId];var teacher=teachers[a.TeacherId];
            return new DutyRow(a.Id,a.TeacherId,exam.ExamDate,$"{teacher.FirstName} {teacher.LastName}",Name(exam),venues[allocation.VenueId].Name,a.PositionIndex+1,a.StartTime,a.EndTime,a.IsLocked,a.Interval.Minutes,allocation.Id,exam.StartTime,exam.EndTime);
        }).OrderBy(x=>x.Date).ThenBy(x=>x.Examination).ThenBy(x=>x.Venue).ThenBy(x=>x.Position).ThenBy(x=>x.Start).ToArray();
        var gaps=new List<UnfilledDuty>();
        foreach(var allocation in request.ExamVenues.Where(x=>exams[x.ExamId].IsActive))
        {
            var exam=exams[allocation.ExamId];
            for(int p=0;p<allocation.RequiredInvigilatorCount;p++)
            {
                var cursor=exam.StartTime;
                foreach(var a in assignments.Where(x=>x.ExamVenueId==allocation.Id && x.PositionIndex==p).OrderBy(x=>x.StartTime))
                {
                    if(a.StartTime>cursor)gaps.Add(new(exam.ExamDate,Name(exam),venues[allocation.VenueId].Name,p+1,cursor,a.StartTime));
                    if(a.EndTime>cursor)cursor=a.EndTime;
                }
                if(cursor<exam.EndTime)gaps.Add(new(exam.ExamDate,Name(exam),venues[allocation.VenueId].Name,p+1,cursor,exam.EndTime));
            }
        }
        var dates=request.Exams.Where(x=>x.IsActive).Select(x=>x.ExamDate).Order().ToArray();
        return new(request.Configuration.SchoolName,dates.Length==0?"No examinations":$"{dates[0]:dd MMM yyyy} – {dates[^1]:dd MMM yyyy}",roster.Name,roster.Status,teachers.Count,teachers.Values.Count(x=>x.IsEligible),request.Exams.Count(x=>x.IsActive),request.ExamVenues.Where(x=>exams[x.ExamId].IsActive).Select(x=>x.VenueId).Distinct().Count(),request.ExamVenues.Where(x=>exams[x.ExamId].IsActive).Sum(x=>(long)x.RequiredInvigilatorCount*exams[x.ExamId].Interval.Minutes),rows,gaps,FairnessCalculator.Calculate(request,assignments.Select(ManualRosterService.ToScheduled).ToArray()),teachers.ToDictionary(x=>x.Key,x=>$"{x.Value.FirstName} {x.Value.LastName}")){TeacherCodes=teachers.ToDictionary(x=>x.Key,x=>x.Value.EmployeeNumber??x.Key.ToString("N")),MinimumBreakMinutes=request.Configuration.MinimumBreakMinutes};
    }
}

public static class ReportTime
{
    public static string Duration(long minutes)=>$"{minutes/60:00}:{minutes%60:00}";
}
public sealed record TeacherHoursRow(Guid TeacherId,string Teacher,int Minutes,double TargetMinutes,int Assignments,int Days)
{
    public double Hours=>Minutes/60.0;
    public double TargetHours=>TargetMinutes/60.0;
    public double DifferenceHours=>Hours-TargetHours;
    public string Duration=>ReportTime.Duration(Minutes);
}
