using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;

public sealed class ValidationTests
{
    internal static SchedulingRequest Request(int teachers=2,int positions=1)
    {
        var venue=new Venue("A1",40);var subject=new Subject("Maths","M");
        var exam=new Exam(subject.Id,8,new(2026,12,10),new(8,0),new(10,0));
        return new() { Teachers=Enumerable.Range(0,teachers).Select(i=>new Teacher("Teacher",i.ToString())).ToArray(),Subjects=[subject],Venues=[venue],Exams=[exam],ExamVenues=[new ExamVenue(exam.Id,venue.Id,30,positions)],Configuration=new() { MinimumBreakMinutes=0 } };
    }
    private static ScheduledAssignment A(SchedulingRequest r,int teacher,int start,int end,int position=0) => new(r.ExamVenues[0].Id,r.Teachers[teacher].Id,position,new(start,0),new(end,0));
    [Fact]
    public void ValidInputsAndCoveragePass()
    {
        var r=Request();Assert.Empty(RequestValidator.Validate(r));
        Assert.Empty(RosterValidator.Validate(r,[A(r,0,8,10)]));
    }
    [Fact]
    public void MissingCoverageAndDuplicateSimultaneousTeacherAreDetected()
    {
        var r=Request(2,2);
        Assert.Contains(RosterValidator.Validate(r,[A(r,0,8,10)]),i=>i.Code=="Coverage");
        Assert.Contains(RosterValidator.Validate(r,[A(r,0,8,10),A(r,0,8,10,1)]),i=>i.Code=="TeacherOverlap");
    }
    [Fact]
    public void CapacityAndSharedVenueConflictsAreRejected()
    {
        var r=Request();var exam=new Exam(r.Subjects[0].Id,9,r.Exams[0].ExamDate,new(9,0),new(11,0));
        r=r with {Exams=[..r.Exams,exam],ExamVenues=[..r.ExamVenues,new(exam.Id,r.Venues[0].Id,41,1)]};
        var issues=RequestValidator.Validate(r);
        Assert.Contains(issues,i=>i.Code=="Capacity");Assert.Contains(issues,i=>i.Code=="VenueOverlap");
    }
    [Fact]
    public void IndependentValidatorChecksAvailabilityLimitsAndBounds()
    {
        var r=Request();r.Teachers[0].MaximumDailyMinutes=60;
        Assert.Contains(RosterValidator.Validate(r,[A(r,0,8,10)]),i=>i.Code=="DailyLimit");
        r.Teachers[0].MaximumDailyMinutes=null;r.Teachers[0].MaximumPeriodMinutes=60;
        Assert.Contains(RosterValidator.Validate(r,[A(r,0,8,10)]),i=>i.Code=="PeriodLimit");
        r.Teachers[0].MaximumPeriodMinutes=null;
        r=r with{Availability=[new(r.Teachers[0].Id,r.Exams[0].ExamDate,new(9,0),new(10,0),false)]};
        Assert.Contains(RosterValidator.Validate(r,[A(r,0,8,10)]),i=>i.Code=="Unavailable");
        Assert.Contains(RosterValidator.Validate(r,[A(r,1,7,10)]),i=>i.Code=="ExamBounds");
    }
    [Fact]
    public void SplitCoverageIsIndependentPerPositionAndMinimumDurationIsEnforced()
    {
        var r=Request();Assert.Empty(RosterValidator.Validate(r,[A(r,0,8,9),A(r,1,9,10)]));
        var shortAssignment=new ScheduledAssignment(r.ExamVenues[0].Id,r.Teachers[0].Id,0,new(8,0),new(8,30));
        Assert.Contains(RosterValidator.Validate(r,[shortAssignment]),i=>i.Code=="SegmentLength");
    }
    [Fact]
    public void LockedConflictsAndMissingLocksAreReported()
    {
        var r=Request(2,2);
        r=r with {LockedAssignments=[A(r,0,8,10),A(r,0,8,10,1)]};
        Assert.Contains(RequestValidator.Validate(r),i=>i.Code=="TeacherOverlap");
        r=Request();r=r with{LockedAssignments=[A(r,0,8,10)]};
        Assert.Contains(RosterValidator.Validate(r,[A(r,1,8,10)]),i=>i.Code=="LockMissing");
    }
    [Fact]
    public void BreaksAndEligibilityAreEnforced()
    {
        var r=Request();var exam=new Exam(r.Subjects[0].Id,9,r.Exams[0].ExamDate,new(10,0),new(12,0));var allocation=new ExamVenue(exam.Id,r.Venues[0].Id,30,1);
        r.Configuration.MinimumBreakMinutes=30;r=r with{Exams=[..r.Exams,exam],ExamVenues=[..r.ExamVenues,allocation]};
        Assert.Contains(RosterValidator.Validate(r,[A(r,0,8,10),new(allocation.Id,r.Teachers[0].Id,0,new(10,0),new(12,0))]),i=>i.Code=="MinimumBreak");
        r.Teachers[0].IsActive=false;
        Assert.Contains(RosterValidator.Validate(r,[A(r,0,8,10)]),i=>i.Code=="Ineligible");
    }
}
