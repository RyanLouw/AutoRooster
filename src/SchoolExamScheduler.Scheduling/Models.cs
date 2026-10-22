using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Scheduling;

public sealed record SchedulingRequest
{
    public required IReadOnlyList<Teacher> Teachers { get; init; }
    public required IReadOnlyList<Subject> Subjects { get; init; }
    public required IReadOnlyList<Exam> Exams { get; init; }
    public required IReadOnlyList<Venue> Venues { get; init; }
    public required IReadOnlyList<ExamVenue> ExamVenues { get; init; }
    public required SchedulingSettings Configuration { get; init; }
    public IReadOnlyList<TeacherAvailability> Availability { get; init; }=[];
    public IReadOnlyList<VenueUnavailability> VenueUnavailability { get; init; }=[];
    public IReadOnlyList<ScheduledAssignment> LockedAssignments { get; init; }=[];
}
public sealed record ScheduledAssignment(Guid ExamVenueId,Guid TeacherId,int PositionIndex,TimeOnly StartTime,TimeOnly EndTime)
{
    public TimeInterval Interval => new(StartTime,EndTime);
}
public sealed record SchedulingIssue(string Code,string Message,Guid? ExamVenueId=null,Guid? TeacherId=null);
public enum SchedulingStatus { Optimal,Feasible,Infeasible,TimeLimitWithoutSolution,Cancelled,InvalidInput,SolverError }
public sealed record TeacherWorkload(Guid TeacherId,decimal Weight,int AvailableMinutes,double TargetMinutes,int ActualMinutes,int AssignmentCount,int DaysAssigned)
{
    public double Difference=>ActualMinutes-TargetMinutes;
    public double? PercentageOfTarget=>TargetMinutes>0 ? ActualMinutes/TargetMinutes*100 : null;
}
public sealed record FairnessStatistics(IReadOnlyList<TeacherWorkload> Teachers)
{
    public int TotalMinutes=>Teachers.Sum(x=>x.ActualMinutes);
    public double Average=>Teachers.Count>0?Teachers.Average(x=>x.ActualMinutes):0;
    public double Median
    {
        get { var a=Teachers.Select(x=>x.ActualMinutes).Order().ToArray();return a.Length==0?0:a.Length%2==1?a[a.Length/2]:(a[a.Length/2-1]+a[a.Length/2])/2.0; }
    }
    public int Minimum=>Teachers.Count>0?Teachers.Min(x=>x.ActualMinutes):0;
    public int Maximum=>Teachers.Count>0?Teachers.Max(x=>x.ActualMinutes):0;
    public double StandardDeviation=>Teachers.Count>0?Math.Sqrt(Teachers.Average(x=>Math.Pow(x.ActualMinutes-Average,2))):0;
    public double MaximumNormalizedDeviation=>Teachers.Where(x=>x.TargetMinutes>0).Select(x=>Math.Abs(x.Difference)/x.TargetMinutes).DefaultIfEmpty(0).Max();
    public int UnusedEligibleTeachers=>Teachers.Count(x=>x.AvailableMinutes>0 && x.ActualMinutes==0);
}
public sealed record SchedulingResult(SchedulingStatus Status,IReadOnlyList<ScheduledAssignment> Assignments,IReadOnlyList<SchedulingIssue> Issues,FairnessStatistics Fairness);
