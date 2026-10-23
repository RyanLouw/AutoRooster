using System.Text.Json;
using SchoolExamScheduler.Licensing;
using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
namespace SchoolExamScheduler.Application;
public sealed class RosterWorkflow(ISchoolDataStore store,InvigilationSolver solver,ILicenceGate? licence=null)
{
    public Guid? LastCreatedRosterId {get;private set;}
    public async Task<SchedulingRequest> LoadRequestAsync(Guid? lockedRosterId=null,CancellationToken cancellationToken=default)
    {
        if(lockedRosterId is not null && (await store.ListAsync<RosterVersion>(cancellationToken)).Single(r=>r.Id==lockedRosterId).IsArchived)
            throw new InvalidOperationException("This exam series is archived. Open its preserved report or generate a roster for the current series.");
        var exams=(await store.ListAsync<Exam>(cancellationToken)).Where(e=>e.IsActive).ToArray();
        var examIds=exams.Select(e=>e.Id).ToHashSet();
        var allocations=(await store.ListAsync<ExamVenue>(cancellationToken)).Where(v=>examIds.Contains(v.ExamId)).ToArray();
        var assignments=lockedRosterId is null ? [] : (await store.ListAsync<InvigilationAssignment>(cancellationToken)).Where(a=>a.RosterVersionId==lockedRosterId).ToArray();
        var teachers=(await store.ListAsync<Teacher>(cancellationToken)).Where(t=>t.IsActive || assignments.Any(a=>a.TeacherId==t.Id)).ToArray();
        var teacherIds=teachers.Select(t=>t.Id).ToHashSet();
        var duties=lockedRosterId is null ? [] : assignments.Where(x=>x.RosterVersionId==lockedRosterId && x.IsLocked && x.AssignmentStatus==AssignmentStatus.Assigned).Select(x=>new ScheduledAssignment(x.ExamVenueId,x.TeacherId,x.PositionIndex,x.StartTime,x.EndTime)).ToArray();
        return new()
        {
            Teachers=teachers,Subjects=await store.ListAsync<Subject>(cancellationToken),Venues=await store.ListAsync<Venue>(cancellationToken),Exams=exams,ExamVenues=allocations,Availability=(await store.ListAsync<TeacherAvailability>(cancellationToken)).Where(a=>teacherIds.Contains(a.TeacherId)).ToArray(),VenueUnavailability=await store.ListAsync<VenueUnavailability>(cancellationToken),Configuration=(await store.ListAsync<SchedulingSettings>(cancellationToken)).SingleOrDefault()??new(),LockedAssignments=duties
        };
    }
    public async Task<SchedulingResult> GenerateAsync(string name,Guid? lockedRosterId,CancellationToken cancellationToken=default)
    {
        if(licence is not null)await licence.RequireWriteAccessAsync(cancellationToken);
        LastCreatedRosterId=null;
        if(cancellationToken.IsCancellationRequested)return new(SchedulingStatus.Cancelled,[],[],new([]));
        if(lockedRosterId is not null)
        {
            var source=(await store.ListAsync<RosterVersion>(cancellationToken)).Single(x=>x.Id==lockedRosterId);
            source.EnsureEditable();
        }
        var request=await LoadRequestAsync(lockedRosterId,cancellationToken);
        if(request.Exams.Count==0)return new(SchedulingStatus.InvalidInput,[],[new("NoExams","Add exams and venue allocations for the next series in Examinations before generating a roster.")],new([]));
        var result=await solver.SolveAsync(request,cancellationToken);
        if(result.Status is not (SchedulingStatus.Optimal or SchedulingStatus.Feasible))return result;
        var issues=RosterValidator.Validate(request,result.Assignments);
        if(issues.Count>0)return new(SchedulingStatus.SolverError,[],issues,result.Fairness);
        var roster=new RosterVersion(name,request.Configuration.SchedulingSeed,"CP-SAT-9.15/v1",JsonSerializer.Serialize(request.Configuration));roster.MarkValidated();
        var duties=result.Assignments.Select(x=>new InvigilationAssignment(roster.Id,x.ExamVenueId,x.TeacherId,x.PositionIndex,x.StartTime,x.EndTime){IsLocked=request.LockedAssignments.Contains(x)}).ToArray();
        await store.CommitAsync([roster,..duties,new AuditRecord("Generate",$"Roster {roster.Id}; result {result.Status}")],[],cancellationToken);
        LastCreatedRosterId=roster.Id;return result;
    }
}
