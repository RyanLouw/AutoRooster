using System.Text.Json;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
namespace SchoolExamScheduler.Application;
public sealed class RosterWorkflow(ISchoolDataStore store,InvigilationSolver solver)
{
    public Guid? LastCreatedRosterId {get;private set;}
    public async Task<SchedulingRequest> LoadRequestAsync(Guid? lockedRosterId=null,CancellationToken cancellationToken=default)
    {
        var duties=lockedRosterId is null ? [] : (await store.ListAsync<InvigilationAssignment>(cancellationToken)).Where(x=>x.RosterVersionId==lockedRosterId && x.IsLocked && x.AssignmentStatus==AssignmentStatus.Assigned).Select(x=>new ScheduledAssignment(x.ExamVenueId,x.TeacherId,x.PositionIndex,x.StartTime,x.EndTime)).ToArray();
        return new()
        {
            Teachers=await store.ListAsync<Teacher>(cancellationToken),Subjects=await store.ListAsync<Subject>(cancellationToken),Venues=await store.ListAsync<Venue>(cancellationToken),Exams=await store.ListAsync<Exam>(cancellationToken),ExamVenues=await store.ListAsync<ExamVenue>(cancellationToken),Availability=await store.ListAsync<TeacherAvailability>(cancellationToken),VenueUnavailability=await store.ListAsync<VenueUnavailability>(cancellationToken),Configuration=(await store.ListAsync<SchedulingSettings>(cancellationToken)).SingleOrDefault()??new(),LockedAssignments=duties
        };
    }
    public async Task<SchedulingResult> GenerateAsync(string name,Guid? lockedRosterId,CancellationToken cancellationToken=default)
    {
        LastCreatedRosterId=null;
        if(cancellationToken.IsCancellationRequested)return new(SchedulingStatus.Cancelled,[],[],new([]));
        if(lockedRosterId is not null)
        {
            var source=(await store.ListAsync<RosterVersion>(cancellationToken)).Single(x=>x.Id==lockedRosterId);
            source.EnsureEditable();
        }
        var request=await LoadRequestAsync(lockedRosterId,cancellationToken);var result=await solver.SolveAsync(request,cancellationToken);
        if(result.Status is not (SchedulingStatus.Optimal or SchedulingStatus.Feasible))return result;
        var issues=RosterValidator.Validate(request,result.Assignments);
        if(issues.Count>0)return new(SchedulingStatus.SolverError,[],issues,result.Fairness);
        var roster=new RosterVersion(name,request.Configuration.SchedulingSeed,"CP-SAT-9.15/v1",JsonSerializer.Serialize(request.Configuration));roster.MarkValidated();
        var duties=result.Assignments.Select(x=>new InvigilationAssignment(roster.Id,x.ExamVenueId,x.TeacherId,x.PositionIndex,x.StartTime,x.EndTime){IsLocked=request.LockedAssignments.Contains(x)}).ToArray();
        await store.CommitAsync([roster,..duties,new AuditRecord("Generate",$"Roster {roster.Id}; result {result.Status}")],[],cancellationToken);
        LastCreatedRosterId=roster.Id;return result;
    }
}
