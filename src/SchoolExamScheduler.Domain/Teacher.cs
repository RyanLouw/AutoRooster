namespace SchoolExamScheduler.Domain;

public sealed class Teacher
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    private string firstName = "", lastName = "";
    private decimal workloadWeight = 1m;
    private int? maximumDailyMinutes, maximumPeriodMinutes;
    public string FirstName { get => firstName; set => firstName = Guard.Text(value,nameof(FirstName)); }
    public string LastName { get => lastName; set => lastName = Guard.Text(value,nameof(LastName)); }
    public string? EmployeeNumber { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public bool CanInvigilate { get; set; } = true;
    public decimal WorkloadWeight { get => workloadWeight; set => workloadWeight = value > 0 && value <= 100 ? value : throw new ArgumentException("Weight must be positive and at most 100."); }
    public int? MaximumDailyMinutes { get => maximumDailyMinutes; set => maximumDailyMinutes = Guard.Limit(value); }
    public int? MaximumPeriodMinutes { get => maximumPeriodMinutes; set => maximumPeriodMinutes = Guard.Limit(value); }
    public string Notes { get; set; } = "";
    public bool IsEligible => IsActive && CanInvigilate;
    private Teacher() { }
    public Teacher(string firstName, string lastName, decimal workloadWeight = 1m,Guid? id=null)
    { Id=Guard.Id(id??Guid.NewGuid());FirstName=firstName; LastName=lastName; WorkloadWeight=workloadWeight; }
}

public sealed class TeacherAvailability
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TeacherId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public bool IsAvailable { get; private set; }
    public string Reason { get; set; } = "";
    public TimeInterval Interval => new(StartTime,EndTime);
    public void Update(Guid teacherId,DateOnly date,TimeOnly start,TimeOnly end,bool isAvailable,string reason)
    {
        Guard.Id(teacherId);_=new TimeInterval(start,end);
        TeacherId=teacherId;Date=date;StartTime=start;EndTime=end;IsAvailable=isAvailable;Reason=reason;
    }
    private TeacherAvailability() { }
    public TeacherAvailability(Guid teacherId,DateOnly date,TimeOnly startTime,TimeOnly endTime,bool isAvailable,Guid? id=null)
    { Id=Guard.Id(id??Guid.NewGuid());TeacherId=Guard.Id(teacherId);Date=date;_ = new TimeInterval(startTime,endTime);StartTime=startTime;EndTime=endTime;IsAvailable=isAvailable; }
}

public static class AvailabilityRules
{
    // Explicit positive records restrict the default window; negative records always win.
    // Adjacent/overlapping positive records form a union, never double-counted.
    public static bool IsAvailable(Guid teacherId, DateOnly date, TimeInterval interval, TimeInterval day, IEnumerable<TeacherAvailability> records)
    {
        if (!day.Contains(interval)) return false;
        var relevant=records.Where(x=>x.TeacherId==teacherId && x.Date==date).ToArray();
        if(relevant.Any(x=>!x.IsAvailable && x.Interval.Overlaps(interval))) return false;
        var positive=relevant.Where(x=>x.IsAvailable).OrderBy(x=>x.StartTime).ToArray();
        if(positive.Length==0) return true;
        var cursor=interval.Start;
        foreach(var record in positive)
        {
            if(record.EndTime<=cursor) continue;
            if(record.StartTime>cursor) return false;
            cursor=record.EndTime;
            if(cursor>=interval.End) return true;
        }
        return false;
    }
}
