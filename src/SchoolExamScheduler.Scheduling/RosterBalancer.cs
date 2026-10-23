using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Scheduling;

// Improve a complete incumbent before CP-SAT spends time on the full interval model.
// Every move preserves fixed coverage; the independent validator checks the result.
internal static class RosterBalancer
{
    private readonly record struct Score(double Worst,double Absolute,int Unused,double Squared,int Segments) : IComparable<Score>
    {
        public int CompareTo(Score other)
        {
            int Compare(double a,double b)=>Math.Abs(a-b)<1e-7?0:a.CompareTo(b);
            var c=Compare(Worst,other.Worst);if(c!=0)return c;
            c=Compare(Absolute,other.Absolute);if(c!=0)return c;
            c=Unused.CompareTo(other.Unused);if(c!=0)return c;
            c=Compare(Squared,other.Squared);return c!=0?c:Segments.CompareTo(other.Segments);
        }
    }
    public static IReadOnlyList<ScheduledAssignment> Improve(SchedulingRequest request,IReadOnlyList<ScheduledAssignment> original,TimeSpan budget,CancellationToken token)
    {
        if(request.Configuration.FairnessWeight==0 || original.Count==0 || budget<=TimeSpan.Zero)return original;
        var clock=System.Diagnostics.Stopwatch.StartNew();var rows=original.ToList();var settings=request.Configuration;
        var targets=FairnessCalculator.Targets(request);var teachers=request.Teachers.Where(t=>t.IsEligible).OrderBy(t=>t.Id).ToArray();
        var exams=request.Exams.ToDictionary(e=>e.Id);var allocations=request.ExamVenues.ToDictionary(v=>v.Id);
        var day=new TimeInterval(settings.DefaultDayStartTime,settings.DefaultDayEndTime);
        var locked=request.LockedAssignments.ToHashSet();
        for(int iteration=0;iteration<2000 && clock.Elapsed<budget && !token.IsCancellationRequested;iteration++)
        {
            var byTeacher=rows.GroupBy(a=>a.TeacherId).ToDictionary(g=>g.Key,g=>g.ToArray());
            var minutes=teachers.ToDictionary(t=>t.Id,t=>byTeacher.GetValueOrDefault(t.Id,[]).Sum(a=>a.Interval.Minutes));
            Score Evaluate(Guid? from=null,Guid? to=null,int transfer=0,int extraSegments=0)
            {
                double worst=0,absolute=0,squared=0;int unused=0;
                foreach(var t in teachers)
                {
                    var actual=minutes[t.Id]+(t.Id==from?-transfer:0)+(t.Id==to?transfer:0);var target=targets[t.Id];
                    var difference=Math.Abs(actual-target);absolute+=difference;
                    if(target>0){var relative=difference/target;worst=Math.Max(worst,relative);squared+=relative*relative;if(actual==0)unused++;}
                }
                return new(worst,absolute,unused,squared,rows.Count+extraSegments);
            }
            bool CanAssign(Teacher teacher,Exam exam,TimeInterval interval)
            {
                if(!AvailabilityRules.IsAvailable(teacher.Id,exam.ExamDate,interval,day,request.Availability))return false;
                var duties=byTeacher.GetValueOrDefault(teacher.Id,[]);
                if(teacher.MaximumPeriodMinutes is int period && minutes[teacher.Id]+interval.Minutes>period)return false;
                var sameDay=duties.Where(a=>exams[allocations[a.ExamVenueId].ExamId].ExamDate==exam.ExamDate).ToArray();
                if(sameDay.Sum(a=>a.Interval.Minutes)+interval.Minutes>(teacher.MaximumDailyMinutes??settings.DefaultMaximumDailyMinutes))return false;
                return sameDay.All(a=>a.EndTime.ToTimeSpan().TotalMinutes+settings.MinimumBreakMinutes<=interval.Start.ToTimeSpan().TotalMinutes || interval.End.ToTimeSpan().TotalMinutes+settings.MinimumBreakMinutes<=a.StartTime.ToTimeSpan().TotalMinutes);
            }
            var before=Evaluate();bool changed=false;
            var recipients=teachers.Where(t=>targets[t.Id]>minutes[t.Id]).OrderBy(t=>minutes[t.Id]/Math.Max(1,targets[t.Id])).ThenBy(t=>t.Id).ToArray();
            foreach(var old in rows.OrderByDescending(a=>minutes[a.TeacherId]/Math.Max(1,targets.GetValueOrDefault(a.TeacherId))).ThenBy(a=>a.ExamVenueId).ThenBy(a=>a.PositionIndex).ThenBy(a=>a.StartTime).ToArray())
            {
                if(locked.Contains(old))continue;
                var exam=exams[allocations[old.ExamVenueId].ExamId];
                foreach(var teacher in recipients)
                {
                    if(clock.Elapsed>=budget || token.IsCancellationRequested)break;
                    if(teacher.Id==old.TeacherId)continue;
                    if(Evaluate(old.TeacherId,teacher.Id,old.Interval.Minutes).CompareTo(before)<0 && CanAssign(teacher,exam,old.Interval))
                    {rows.Remove(old);rows.Add(old with{TeacherId=teacher.Id});changed=true;break;}
                    if(!settings.AllowSessionSplitting || rows.Count(a=>a.ExamVenueId==old.ExamVenueId && a.PositionIndex==old.PositionIndex)>=settings.MaximumSessionSegmentsPerPosition)continue;
                    var boundaries=new SortedSet<TimeOnly>();
                    for(var time=exam.StartTime.ToTimeSpan().TotalMinutes+settings.SchedulingTimeIncrementMinutes;time<exam.EndTime.ToTimeSpan().TotalMinutes;time+=settings.SchedulingTimeIncrementMinutes)boundaries.Add(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(time)));
                    foreach(var available in request.Availability.Where(a=>a.Date==exam.ExamDate)){boundaries.Add(available.StartTime);boundaries.Add(available.EndTime);}
                    foreach(var split in boundaries.Where(t=>t>old.StartTime && t<old.EndTime))
                    {
                        var first=new TimeInterval(old.StartTime,split);var second=new TimeInterval(split,old.EndTime);
                        if(first.Minutes<settings.MinimumSessionMinutes || second.Minutes<settings.MinimumSessionMinutes)continue;
                        foreach(var transferFirst in new[]{true,false})
                        {
                            var transfer=transferFirst?first:second;
                            if(Evaluate(old.TeacherId,teacher.Id,transfer.Minutes,1).CompareTo(before)>=0 || !CanAssign(teacher,exam,transfer))continue;
                            rows.Remove(old);rows.Add(old with{StartTime=first.Start,EndTime=first.End,TeacherId=transferFirst?teacher.Id:old.TeacherId});
                            rows.Add(old with{StartTime=second.Start,EndTime=second.End,TeacherId=transferFirst?old.TeacherId:teacher.Id});changed=true;break;
                        }
                        if(changed)break;
                    }
                    if(changed)break;
                }
                if(changed)break;
            }
            if(!changed)break;
        }
        return RosterValidator.Validate(request,rows).Count==0?rows:original;
    }
}
