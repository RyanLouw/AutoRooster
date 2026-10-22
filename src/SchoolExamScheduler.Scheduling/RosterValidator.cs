using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Scheduling;

// Independent output validation: chronological sweeps and exact coverage partitions,
// rather than the solver's candidate variable constraints.
public static class RosterValidator
{
    public static IReadOnlyList<SchedulingIssue> Validate(SchedulingRequest request,IReadOnlyList<ScheduledAssignment> assignments,bool requireComplete=true,bool checkLocks=true)
    {
        var issues=new List<SchedulingIssue>();
        var teachers=request.Teachers.ToDictionary(x=>x.Id);var exams=request.Exams.ToDictionary(x=>x.Id);var allocations=request.ExamVenues.ToDictionary(x=>x.Id);
        var valid=new List<(ScheduledAssignment Assignment,Exam Exam,int Minutes)>();
        foreach(var a in assignments)
        {
            if(!teachers.TryGetValue(a.TeacherId,out var teacher)||!allocations.TryGetValue(a.ExamVenueId,out var allocation)||!exams.TryGetValue(allocation.ExamId,out var exam)) {issues.Add(new("Reference","Assignment references an unknown teacher or exam venue.",a.ExamVenueId,a.TeacherId));continue;}
            if(a.StartTime>=a.EndTime) {issues.Add(new("Interval","Assignment has invalid times.",a.ExamVenueId,a.TeacherId));continue;}
            TimeInterval interval;
            try{interval=a.Interval;}catch(ArgumentException e){issues.Add(new("Interval",e.Message,a.ExamVenueId,a.TeacherId));continue;}
            var venue=request.Venues.FirstOrDefault(x=>x.Id==allocation.VenueId);
            if(venue is null || !venue.IsActive || request.VenueUnavailability.Any(x=>x.VenueId==allocation.VenueId && x.Date==exam.ExamDate && x.Interval.Overlaps(interval)))issues.Add(new("VenueUnavailable","The assigned physical venue is unavailable.",a.ExamVenueId,a.TeacherId));
            valid.Add((a,exam,interval.Minutes));
            if(!teacher.IsEligible)issues.Add(new("Ineligible","Inactive or ineligible teacher assigned.",a.ExamVenueId,a.TeacherId));
            if(!exam.IsActive || !exam.Interval.Contains(interval))issues.Add(new("ExamBounds","Assignment is outside an active examination.",a.ExamVenueId,a.TeacherId));
            if(a.PositionIndex<0 || a.PositionIndex>=allocation.RequiredInvigilatorCount)issues.Add(new("Position","Invalid independent supervision position.",a.ExamVenueId,a.TeacherId));
            if(!AvailabilityRules.IsAvailable(a.TeacherId,exam.ExamDate,interval,new(request.Configuration.DefaultDayStartTime,request.Configuration.DefaultDayEndTime),request.Availability))issues.Add(new("Unavailable","Assignment conflicts with teacher availability.",a.ExamVenueId,a.TeacherId));
            if(interval!=exam.Interval && interval.Minutes<request.Configuration.MinimumSessionMinutes)issues.Add(new("SegmentLength","Split segment is shorter than the configured minimum.",a.ExamVenueId,a.TeacherId));
            if(!request.Configuration.AllowSessionSplitting && interval!=exam.Interval)issues.Add(new("SplittingDisabled","Split sessions are disabled.",a.ExamVenueId,a.TeacherId));
        }
        foreach(var group in valid.GroupBy(x=>x.Assignment.TeacherId))
        {
            var teacher=teachers[group.Key];
            if(teacher.MaximumPeriodMinutes is int maximum && group.Sum(x=>x.Minutes)>maximum)issues.Add(new("PeriodLimit","Teacher exceeds the examination-period limit.",TeacherId:teacher.Id));
            foreach(var day in group.GroupBy(x=>x.Exam.ExamDate))
            {
                if(day.Sum(x=>x.Minutes)>(teacher.MaximumDailyMinutes??request.Configuration.DefaultMaximumDailyMinutes))issues.Add(new("DailyLimit","Teacher exceeds the daily limit.",TeacherId:teacher.Id));
                var sorted=day.OrderBy(x=>x.Assignment.StartTime).ThenBy(x=>x.Assignment.EndTime).ToArray();
                for(int i=1;i<sorted.Length;i++)
                {
                    var previous=sorted[i-1].Assignment;var current=sorted[i].Assignment;
                    var gap=(current.StartTime.ToTimeSpan()-previous.EndTime.ToTimeSpan()).TotalMinutes;
                    if(gap<0)issues.Add(new("TeacherOverlap","Teacher covers simultaneous duties.",current.ExamVenueId,teacher.Id));
                    else if(gap<request.Configuration.MinimumBreakMinutes)issues.Add(new("MinimumBreak","Insufficient rest between duties.",current.ExamVenueId,teacher.Id));
                }
            }
        }
        foreach(var allocation in request.ExamVenues.Where(x=>exams[x.ExamId].IsActive))
        {
            var exam=exams[allocation.ExamId];
            for(var position=0;position<allocation.RequiredInvigilatorCount;position++)
            {
                var segments=valid.Where(x=>x.Assignment.ExamVenueId==allocation.Id && x.Assignment.PositionIndex==position).Select(x=>x.Assignment).OrderBy(x=>x.StartTime).ToArray();
                if(segments.Length>request.Configuration.MaximumSessionSegmentsPerPosition)issues.Add(new("SegmentCount","Too many segments for one supervision position.",allocation.Id));
                var cursor=exam.StartTime;
                foreach(var segment in segments)
                {
                    if(segment.StartTime<cursor || (requireComplete && segment.StartTime!=cursor))issues.Add(new("Coverage","Position has overlapping segments or a coverage gap.",allocation.Id));
                    cursor=segment.EndTime;
                }
                if(requireComplete && cursor!=exam.EndTime)issues.Add(new("Coverage",$"Position {position+1} is not fully covered on {exam.ExamDate:yyyy-MM-dd} between {exam.StartTime:HH:mm} and {exam.EndTime:HH:mm}.",allocation.Id));
            }
        }
        if(checkLocks)foreach(var locked in request.LockedAssignments)
            if(assignments.Count(x=>x==locked)!=1)issues.Add(new("LockMissing","A locked assignment was changed or removed.",locked.ExamVenueId,locked.TeacherId));
        return issues;
    }
}
