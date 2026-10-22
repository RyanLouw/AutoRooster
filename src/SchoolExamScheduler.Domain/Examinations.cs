namespace SchoolExamScheduler.Domain;

public sealed class Subject
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    private string name="", subjectCode="";
    public string Name { get=>name;set=>name=Guard.Text(value,nameof(Name)); }
    public string SubjectCode { get=>subjectCode;set=>subjectCode=Guard.Text(value,nameof(SubjectCode)); }
    public bool IsActive { get; set; } = true;
    private Subject() { }
    public Subject(string name,string subjectCode,Guid? id=null) { Id=Guard.Id(id??Guid.NewGuid());Name=name;SubjectCode=subjectCode; }
}
public sealed class Venue
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    private string name="";private int capacity;
    public string Name { get=>name;set=>name=Guard.Text(value,nameof(Name)); }
    public int Capacity { get=>capacity;set=>capacity=Guard.Positive(value,nameof(Capacity)); }
    public bool IsActive { get; set; } = true;
    public string Notes { get; set; } = "";
    private Venue() { }
    public Venue(string name,int capacity,Guid? id=null) { Id=Guard.Id(id??Guid.NewGuid());Name=name;Capacity=capacity; }
}
public sealed class VenueUnavailability
{
    public Guid Id { get; private set; }=Guid.NewGuid();
    public Guid VenueId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public string Reason { get; set; }="";
    public TimeInterval Interval=>new(StartTime,EndTime);
    private VenueUnavailability() { }
    public VenueUnavailability(Guid venueId,DateOnly date,TimeOnly start,TimeOnly end)
    { VenueId=Guard.Id(venueId);Date=date;_=new TimeInterval(start,end);StartTime=start;EndTime=end; }
}
public sealed class Exam
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SubjectId { get; private set; }
    public int Grade { get; private set; }
    public DateOnly ExamDate { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public string Description { get; set; }="";
    public bool IsActive { get; set; }=true;
    public TimeInterval Interval=>new(StartTime,EndTime);
    public void Update(Guid subjectId,int grade,DateOnly date,TimeOnly start,TimeOnly end)
    {Guard.Id(subjectId);Guard.Positive(grade,nameof(Grade));_=new TimeInterval(start,end);SubjectId=subjectId;Grade=grade;ExamDate=date;StartTime=start;EndTime=end;}
    private Exam() { }
    public Exam(Guid subjectId,int grade,DateOnly examDate,TimeOnly startTime,TimeOnly endTime,Guid? id=null)
    { Id=Guard.Id(id??Guid.NewGuid());SubjectId=Guard.Id(subjectId);Grade=Guard.Positive(grade,nameof(Grade));ExamDate=examDate;_=new TimeInterval(startTime,endTime);StartTime=startTime;EndTime=endTime; }
}
public sealed class ExamVenue
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ExamId { get; private set; }
    public Guid VenueId { get; private set; }
    public int ExpectedStudentCount { get; private set; }
    public int RequiredInvigilatorCount { get; private set; }
    private ExamVenue() { }
    public ExamVenue(Guid examId,Guid venueId,int expectedStudentCount,int requiredInvigilatorCount,Guid? id=null)
    { Id=Guard.Id(id??Guid.NewGuid());ExamId=Guard.Id(examId);VenueId=Guard.Id(venueId);ExpectedStudentCount=Guard.Positive(expectedStudentCount,nameof(ExpectedStudentCount));RequiredInvigilatorCount=Guard.Positive(requiredInvigilatorCount,nameof(RequiredInvigilatorCount)); }
    public void ValidateCapacity(Venue venue)
    { if(venue.Id!=VenueId || ExpectedStudentCount>venue.Capacity || !venue.IsActive) throw new ArgumentException("Venue is inactive, mismatched, or over capacity."); }
}
