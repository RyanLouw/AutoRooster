using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Scheduling;

public static class FairnessCalculator
{
    public static IReadOnlyDictionary<Guid,int> Capacities(SchedulingRequest request)
    {
        var day=new TimeInterval(request.Configuration.DefaultDayStartTime,request.Configuration.DefaultDayEndTime);
        var demand=request.Exams.Where(e=>e.IsActive && request.ExamVenues.Any(v=>v.ExamId==e.Id)).GroupBy(e=>e.ExamDate).ToArray();
        return request.Teachers.Where(t=>t.IsEligible).ToDictionary(t=>t.Id,t=>
        {
            int total=0;
            foreach(var date in demand)
            {
                var eligible=0;
                // Count each demanded wall-clock minute once, regardless of venue multiplicity.
                for(int minute=0;minute<1439;minute++)
                {
                    var interval=new TimeInterval(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute)),TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute+1)));
                    if(date.Any(e=>e.Interval.Contains(interval)) && AvailabilityRules.IsAvailable(t.Id,date.Key,interval,day,request.Availability))eligible++;
                }
                total=checked(total+Math.Min(eligible,t.MaximumDailyMinutes??request.Configuration.DefaultMaximumDailyMinutes));
            }
            return Math.Min(total,t.MaximumPeriodMinutes??int.MaxValue);
        });
    }
    public static IReadOnlyDictionary<Guid,double> Targets(SchedulingRequest request,IReadOnlyDictionary<Guid,int>? capacities=null)
    {
        capacities??=Capacities(request);
        var totalDemand=request.ExamVenues.Sum(v=>(long)request.Exams.Single(e=>e.Id==v.ExamId).Interval.Minutes*v.RequiredInvigilatorCount*(request.Exams.Single(e=>e.Id==v.ExamId).IsActive?1:0));
        var shares=request.Teachers.Where(t=>t.IsEligible).ToDictionary(t=>t.Id,t=>(double)t.WorkloadWeight*capacities[t.Id]);
        var sum=shares.Values.Sum();return shares.ToDictionary(x=>x.Key,x=>sum>0?totalDemand*x.Value/sum:0);
    }
    public static FairnessStatistics Calculate(SchedulingRequest request,IReadOnlyList<ScheduledAssignment> assignments)
    {
        var capacities=Capacities(request);var targets=Targets(request,capacities);
        var dates=request.ExamVenues.ToDictionary(v=>v.Id,v=>request.Exams.Single(e=>e.Id==v.ExamId).ExamDate);
        return new(request.Teachers.Where(t=>t.IsEligible || assignments.Any(a=>a.TeacherId==t.Id)).OrderBy(t=>t.Id).Select(t=>
        {
            var a=assignments.Where(x=>x.TeacherId==t.Id).ToArray();
            return new TeacherWorkload(t.Id,t.WorkloadWeight,capacities.GetValueOrDefault(t.Id),targets.GetValueOrDefault(t.Id),a.Sum(x=>x.Interval.Minutes),a.Length,a.Select(x=>dates[x.ExamVenueId]).Distinct().Count());
        }).ToArray());
    }
}
