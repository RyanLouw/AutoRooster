using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
namespace SchoolExamScheduler.Application;
public sealed class ManualRosterService(ISchoolDataStore store,RosterWorkflow workflow)
{
    private static InvigilationAssignment New(InvigilationAssignment original,Guid teacher,TimeOnly start,TimeOnly end)=>new(original.RosterVersionId,original.ExamVenueId,teacher,original.PositionIndex,start,end){IsManual=true,Notes=original.Notes};
    private async Task EditAsync(Guid rosterId,string action,Action<List<InvigilationAssignment>> edit)
    {
        var roster=(await store.ListAsync<RosterVersion>()).Single(x=>x.Id==rosterId);roster.EnsureEditable();
        var original=(await store.ListAsync<InvigilationAssignment>()).Where(x=>x.RosterVersionId==rosterId && x.AssignmentStatus==AssignmentStatus.Assigned).ToArray();
        var changed=original.ToList();edit(changed);
        foreach(var locked in original.Where(x=>x.IsLocked))if(!changed.Contains(locked))throw new InvalidOperationException("Unlock the assignment before changing it.");
        var request=await workflow.LoadRequestAsync(rosterId);var output=changed.Select(ToScheduled).ToArray();
        var issues=RosterValidator.Validate(request,output,requireComplete:false);
        if(issues.Count>0)throw new ArgumentException(string.Join(Environment.NewLine,issues.Select(x=>x.Message)));
        var complete=RosterValidator.Validate(request,output);
        if(complete.Count==0)roster.MarkValidated();else roster.MarkIncomplete();
        await store.CommitAsync([roster,..changed.Except(original),new AuditRecord(action,$"Roster {rosterId}; removed {string.Join(',',original.Except(changed).Select(x=>x.Id))}; added {string.Join(',',changed.Except(original).Select(x=>x.Id))}")],original.Except(changed).Cast<object>().ToArray());
    }
    public static ScheduledAssignment ToScheduled(InvigilationAssignment a)=>new(a.ExamVenueId,a.TeacherId,a.PositionIndex,a.StartTime,a.EndTime);
    public Task ReplaceAsync(Guid rosterId,Guid assignmentId,Guid teacherId)=>EditAsync(rosterId,"Replace teacher",rows=>
    {var old=rows.Single(x=>x.Id==assignmentId);rows.Remove(old);rows.Add(New(old,teacherId,old.StartTime,old.EndTime));});
    public Task SwapAsync(Guid rosterId,Guid firstId,Guid secondId)=>EditAsync(rosterId,"Swap teachers",rows=>
    {if(firstId==secondId)throw new ArgumentException("Select two different duties.");var first=rows.Single(x=>x.Id==firstId);var second=rows.Single(x=>x.Id==secondId);rows.Remove(first);rows.Remove(second);rows.Add(New(first,second.TeacherId,first.StartTime,first.EndTime));rows.Add(New(second,first.TeacherId,second.StartTime,second.EndTime));});
    public Task SplitAsync(Guid rosterId,Guid assignmentId,TimeOnly handover,Guid secondTeacherId)=>EditAsync(rosterId,"Split session",rows=>
    {var old=rows.Single(x=>x.Id==assignmentId);if(handover<=old.StartTime || handover>=old.EndTime)throw new ArgumentException("Handover must be inside the assignment interval.");rows.Remove(old);rows.Add(New(old,old.TeacherId,old.StartTime,handover));rows.Add(New(old,secondTeacherId,handover,old.EndTime));});
    public Task CombineAsync(Guid rosterId,Guid firstId,Guid secondId)=>EditAsync(rosterId,"Combine sessions",rows=>
    {if(firstId==secondId)throw new ArgumentException("Select two different duties.");var first=rows.Single(x=>x.Id==firstId);var second=rows.Single(x=>x.Id==secondId);if(second.StartTime<first.StartTime)(first,second)=(second,first);if(first.ExamVenueId!=second.ExamVenueId || first.TeacherId!=second.TeacherId || first.PositionIndex!=second.PositionIndex || first.EndTime!=second.StartTime)throw new ArgumentException("Only adjacent duties for the same teacher and position can be combined.");rows.Remove(first);rows.Remove(second);rows.Add(New(first,first.TeacherId,first.StartTime,second.EndTime));});
    public Task RemoveAsync(Guid rosterId,Guid assignmentId)=>EditAsync(rosterId,"Remove duty",rows=>rows.Remove(rows.Single(x=>x.Id==assignmentId)));
    public async Task SetLockAsync(Guid rosterId,Guid assignmentId,bool locked)
    {
        var roster=(await store.ListAsync<RosterVersion>()).Single(x=>x.Id==rosterId);roster.EnsureEditable();
        var duty=(await store.ListAsync<InvigilationAssignment>()).Single(x=>x.RosterVersionId==rosterId && x.Id==assignmentId);
        duty.IsLocked=locked;await store.CommitAsync([duty,new AuditRecord(locked?"Lock":"Unlock",$"Assignment {assignmentId}")],[]);
    }
    public async Task<IReadOnlyList<SchedulingIssue>> ValidateAsync(Guid rosterId)
    {
        var request=await workflow.LoadRequestAsync(rosterId);var duties=(await store.ListAsync<InvigilationAssignment>()).Where(x=>x.RosterVersionId==rosterId && x.AssignmentStatus==AssignmentStatus.Assigned).Select(ToScheduled).ToArray();return RosterValidator.Validate(request,duties);
    }
    public async Task FinalizeAsync(Guid rosterId)
    {
        var roster=(await store.ListAsync<RosterVersion>()).Single(x=>x.Id==rosterId);roster.EnsureEditable();
        var issues=await ValidateAsync(rosterId);if(issues.Count>0)throw new InvalidOperationException("Roster cannot be finalized: "+string.Join(Environment.NewLine,issues.Select(x=>x.Message)));
        roster.MarkValidated();roster.FinalizeRoster(true);await store.CommitAsync([roster,new AuditRecord("Finalize",$"Roster {rosterId}")],[]);
    }
}
