namespace SchoolExamScheduler.Domain;
public enum RosterStatus { Draft,Incomplete,Validated,Finalized }
public enum AssignmentStatus { Assigned,Cancelled }
public sealed class RosterVersion
{
    public Guid Id { get; private set; }=Guid.NewGuid();
    public string Name { get; private set; }
    public DateTimeOffset CreatedOn { get; private set; }=DateTimeOffset.UtcNow;
    public RosterStatus Status { get; private set; }
    public int SchedulingSeed { get; private set; }
    public string AlgorithmVersion { get; private set; }
    public string SettingsSnapshot { get; private set; }
    public RosterVersion(string name,int schedulingSeed,string algorithmVersion,string settingsSnapshot)
    { Name=Guard.Text(name,nameof(Name));SchedulingSeed=schedulingSeed;AlgorithmVersion=Guard.Text(algorithmVersion,nameof(AlgorithmVersion));SettingsSnapshot=Guard.Text(settingsSnapshot,nameof(SettingsSnapshot)); }
    public void EnsureEditable() { if(Status==RosterStatus.Finalized) throw new InvalidOperationException("Finalized rosters cannot be changed. Create a new version."); }
    public void MarkValidated() { EnsureEditable();Status=RosterStatus.Validated; }
    public void MarkIncomplete() { EnsureEditable();Status=RosterStatus.Incomplete; }
    public void MarkDraft() { EnsureEditable();Status=RosterStatus.Draft; }
    public void FinalizeRoster(bool coverageValidated)
    { EnsureEditable();if(!coverageValidated || Status!=RosterStatus.Validated) throw new InvalidOperationException("Validate complete coverage before finalizing.");Status=RosterStatus.Finalized; }
}
public sealed class InvigilationAssignment
{
    public Guid Id { get; private set; }=Guid.NewGuid();
    public Guid RosterVersionId { get; private set; }
    public Guid ExamVenueId { get; private set; }
    public Guid TeacherId { get; private set; }
    public int PositionIndex { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public bool IsManual { get; set; }
    public bool IsLocked { get; set; }
    public AssignmentStatus AssignmentStatus { get; set; }=AssignmentStatus.Assigned;
    public string Notes { get; set; }="";
    public TimeInterval Interval=>new(StartTime,EndTime);
    private InvigilationAssignment() { }
    public InvigilationAssignment(Guid rosterVersionId,Guid examVenueId,Guid teacherId,int positionIndex,TimeOnly startTime,TimeOnly endTime)
    { RosterVersionId=Guard.Id(rosterVersionId);ExamVenueId=Guard.Id(examVenueId);TeacherId=Guard.Id(teacherId);if(positionIndex<0) throw new ArgumentException("Position index must be nonnegative.");PositionIndex=positionIndex;_=new TimeInterval(startTime,endTime);StartTime=startTime;EndTime=endTime; }
}
public sealed class AuditRecord
{
    public Guid Id { get; private set; }=Guid.NewGuid();
    public DateTimeOffset CreatedOn { get; private set; }=DateTimeOffset.UtcNow;
    public string Action { get; private set; }
    public string Detail { get; private set; }
    public AuditRecord(string action,string detail) {Action=Guard.Text(action,nameof(Action));Detail=Guard.Text(detail,nameof(Detail));}
}
