using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;
public sealed class ValidationBoundaryTests
{
    [Fact]
    public void IndependentValidatorRejectsVenueRestrictionsAndSubMinuteTimes()
    {
        var r=ValidationTests.Request();var assignment=new ScheduledAssignment(r.ExamVenues[0].Id,r.Teachers[0].Id,0,new(8,0),new(10,0));
        r=r with{VenueUnavailability=[new(r.Venues[0].Id,r.Exams[0].ExamDate,new(9,0),new(10,0))]};
        Assert.Contains(RosterValidator.Validate(r,[assignment]),x=>x.Code=="VenueUnavailable");
        var invalid=assignment with{StartTime=new(8,0,30)};
        Assert.Contains(RosterValidator.Validate(r,[invalid]),x=>x.Code=="Interval");
    }
    [Fact]
    public void InactiveArchivedExamsDoNotBlockActiveScheduling()
    {
        var r=ValidationTests.Request();r.Exams[0].IsActive=false;r.Subjects[0].IsActive=false;r.Venues[0].IsActive=false;
        Assert.Empty(RequestValidator.Validate(r));
    }
}
