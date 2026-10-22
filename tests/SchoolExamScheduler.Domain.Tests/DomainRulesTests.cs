using SchoolExamScheduler.Domain;
using Xunit;

public sealed class DomainRulesTests
{
    private static TimeOnly T(int h, int m = 0) => new(h, m);
    [Theory]
    [InlineData(8,10,10,12,false)]
    [InlineData(8,10,9,11,true)]
    [InlineData(8,10,7,8,false)]
    [InlineData(8,10,8,10,true)]
    public void IntervalsUseHalfOpenSemantics(int a,int b,int c,int d,bool expected) =>
        Assert.Equal(expected, new TimeInterval(T(a),T(b)).Overlaps(new(T(c),T(d))));
    [Theory]
    [InlineData(8,8)]
    [InlineData(23,1)]
    public void InvalidIntervalsAreRejected(int a,int b) => Assert.Throws<ArgumentException>(() => new TimeInterval(T(a),T(b)));
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WeightMustBePositive(double weight) => Assert.Throws<ArgumentException>(() => new Teacher("A","B",(decimal)weight));
    [Fact]
    public void TeacherRequiresNamesAndHasEligibilityAndLimits()
    {
        Assert.Throws<ArgumentException>(() => new Teacher(" ","B"));
        var teacher = new Teacher("A","B");
        Assert.True(teacher.IsEligible);
        teacher.CanInvigilate = false;
        Assert.False(teacher.IsEligible);
        Assert.Throws<ArgumentException>(() => teacher.MaximumDailyMinutes = -1);
        Assert.Throws<ArgumentException>(() => teacher.MaximumPeriodMinutes = 0);
    }
    [Fact]
    public void CapacityAndIndependentPositionsAreValidated()
    {
        var venue = new Venue("A1",30);
        var allocation = new ExamVenue(Guid.NewGuid(),venue.Id,30,2);
        allocation.ValidateCapacity(venue);
        Assert.Equal(2,allocation.RequiredInvigilatorCount);
        Assert.Throws<ArgumentException>(() => new ExamVenue(Guid.NewGuid(),venue.Id,31,1).ValidateCapacity(venue));
        Assert.Throws<ArgumentException>(() => new ExamVenue(Guid.NewGuid(),venue.Id,1,0));
        Assert.Throws<ArgumentException>(() => new Venue("A2",0));
    }
    [Fact]
    public void AvailabilityDefaultsToDayWindowAndUnavailabilityWins()
    {
        var id=Guid.NewGuid();var date=new DateOnly(2026,12,10);
        var day = new TimeInterval(T(7),T(16));
        var records = new[] {new TeacherAvailability(id,date,T(8),T(12),true),new TeacherAvailability(id,date,T(9),T(10),false)};
        Assert.True(AvailabilityRules.IsAvailable(id,date,new(T(8),T(9)),day,records));
        Assert.False(AvailabilityRules.IsAvailable(id,date,new(T(8),T(11)),day,records));
        Assert.False(AvailabilityRules.IsAvailable(id,date,new(T(6),T(8)),day,[]));
        Assert.True(AvailabilityRules.IsAvailable(id,date,new(T(12),T(13)),day,[]));
        Assert.False(AvailabilityRules.IsAvailable(id,date,new(T(12),T(13)),day,records));
    }
    [Fact]
    public void FinalizedRostersCannotBeChangedOrRegenerated()
    {
        var roster = new RosterVersion("December",123,"v1","{}");
        Assert.Throws<InvalidOperationException>(()=>roster.FinalizeRoster(false));
        roster.MarkValidated();roster.FinalizeRoster(true);
        Assert.Equal(RosterStatus.Finalized,roster.Status);
        Assert.Throws<InvalidOperationException>(roster.EnsureEditable);
        Assert.Throws<InvalidOperationException>(roster.MarkIncomplete);
    }
    [Fact]
    public void SettingsRejectInvalidSplitAndBreakLimits()
    {
        var settings = new SchedulingSettings();settings.Validate();
        settings.MinimumBreakMinutes=-1;
        Assert.Throws<ArgumentException>(settings.Validate);
        settings.MinimumBreakMinutes=0;settings.MinimumSessionMinutes=0;
        Assert.Throws<ArgumentException>(settings.Validate);
    }
    [Fact]
    public void AssignmentsCarryIndependentPositionAndExactInterval()
    {
        var assignment = new InvigilationAssignment(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,T(8),T(9));
        Assert.Equal(60,assignment.Interval.Minutes);
        Assert.Equal(1,assignment.PositionIndex);
        Assert.Throws<ArgumentException>(()=> new InvigilationAssignment(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),-1,T(8),T(9)));
    }
    [Fact]
    public void ExamAndSubjectValidateRequiredFields()
    {
        var subject = new Subject("Mathematics","MATH");
        var exam = new Exam(subject.Id,8,new(2026,12,10),T(8),T(10));
        Assert.Equal(120,exam.Interval.Minutes);
        Assert.Throws<ArgumentException>(()=>new Subject(" ","X"));
        Assert.Throws<ArgumentException>(()=>new Exam(subject.Id,0,new(2026,12,10),T(8),T(10)));
    }
}
