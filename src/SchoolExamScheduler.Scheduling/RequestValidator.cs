using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Scheduling;

public static class RequestValidator
{
    public static IReadOnlyList<SchedulingIssue> Validate(SchedulingRequest request)
    {
        var issues=new List<SchedulingIssue>();
        try {request.Configuration.Validate();} catch(ArgumentException e) {issues.Add(new("Settings",e.Message));}
        void Duplicates(IEnumerable<Guid> ids,string name) {if(ids.Distinct().Count()!=ids.Count()) issues.Add(new("DuplicateId",$"Duplicate {name} identifiers."));}
        Duplicates(request.Teachers.Select(x=>x.Id),"teacher");Duplicates(request.Exams.Select(x=>x.Id),"exam");Duplicates(request.Venues.Select(x=>x.Id),"venue");Duplicates(request.Subjects.Select(x=>x.Id),"subject");Duplicates(request.ExamVenues.Select(x=>x.Id),"allocation");
        if(issues.Count>0)return issues;
        var exams=request.Exams.ToDictionary(x=>x.Id);var venues=request.Venues.ToDictionary(x=>x.Id);
        var demand=request.ExamVenues.Where(v=>exams.ContainsKey(v.ExamId) && exams[v.ExamId].IsActive).Sum(v=>(long)v.RequiredInvigilatorCount*exams[v.ExamId].Interval.Minutes);
        if(demand>1_000_000)issues.Add(new("SizeLimit","This roster exceeds the supported fixed-point objective limit of 1,000,000 teacher-minutes. Select a smaller examination period."));
        foreach(var exam in request.Exams)
        {
            if(exam.IsActive && !request.Subjects.Any(x=>x.Id==exam.SubjectId && x.IsActive)) issues.Add(new("Subject","An examination has a missing or inactive subject."));
            if(exam.IsActive && !request.ExamVenues.Any(x=>x.ExamId==exam.Id)) issues.Add(new("NoVenue","An active examination has no venue."));
            if(!new TimeInterval(request.Configuration.DefaultDayStartTime,request.Configuration.DefaultDayEndTime).Contains(exam.Interval)) issues.Add(new("DayBounds","An examination is outside the configured day window."));
        }
        foreach(var allocation in request.ExamVenues)
        {
            if(!exams.TryGetValue(allocation.ExamId,out var exam) || !venues.TryGetValue(allocation.VenueId,out var venue)) {issues.Add(new("Reference","An allocation references a missing exam or venue.",allocation.Id));continue;}
            if(!exam.IsActive)continue;
            try{allocation.ValidateCapacity(venue);}catch(ArgumentException e){issues.Add(new("Capacity",e.Message,allocation.Id));}
            if(request.VenueUnavailability.Any(x=>x.VenueId==venue.Id && x.Date==exam.ExamDate && x.Interval.Overlaps(exam.Interval))) issues.Add(new("VenueUnavailable","Venue is unavailable during this examination.",allocation.Id));
        }
        foreach(var group in request.ExamVenues.GroupBy(x=>x.VenueId))
        {
            var a=group.Where(x=>exams.ContainsKey(x.ExamId)).ToArray();
            for(var i=0;i<a.Length;i++)for(var j=i+1;j<a.Length;j++)
            {
                var x=exams[a[i].ExamId];var y=exams[a[j].ExamId];
                if(x.IsActive && y.IsActive && x.ExamDate==y.ExamDate && x.Interval.Overlaps(y.Interval)) issues.Add(new("VenueOverlap","The same physical venue is allocated to overlapping examinations.",a[j].Id));
            }
        }
        if(request.Availability.Any(x=>!request.Teachers.Any(t=>t.Id==x.TeacherId)) || request.VenueUnavailability.Any(x=>!venues.ContainsKey(x.VenueId))) issues.Add(new("Reference","Availability references a missing teacher or venue."));
        if(issues.Count==0)issues.AddRange(RosterValidator.Validate(request,request.LockedAssignments,requireComplete:false,checkLocks:false));
        return issues;
    }
}
