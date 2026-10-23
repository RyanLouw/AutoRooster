namespace SchoolExamScheduler.Domain;
public sealed class SchedulingSettings
{
    public int Id { get; set; }=1;
    public bool AutomaticBackupBeforeChanges { get; set; }
    public string SchoolName { get; set; }="School";
    public TimeOnly DefaultDayStartTime { get; set; }=new(7,0);
    public TimeOnly DefaultDayEndTime { get; set; }=new(17,0);
    public int MinimumBreakMinutes { get; set; }=15;
    public int DefaultMaximumDailyMinutes { get; set; }=360;
    public bool AllowSessionSplitting { get; set; }=true;
    public int MinimumSessionMinutes { get; set; }=30;
    public int MaximumSessionSegmentsPerPosition { get; set; }=3;
    public int SchedulingTimeIncrementMinutes { get; set; }=15;
    public int FairnessWeight { get; set; }=100;
    public int DailyBalanceWeight { get; set; }=10;
    public int DutyCountBalanceWeight { get; set; }=1;
    public int UnassignedTeacherPenalty { get; set; }=10;
    public int SolverTimeLimitSeconds { get; set; }=30;
    public int SchedulingSeed { get; set; }=42;
    public void Validate()
    {
        _=new TimeInterval(DefaultDayStartTime,DefaultDayEndTime);
        if(MinimumBreakMinutes<0 || DefaultMaximumDailyMinutes<=0 || MinimumSessionMinutes<=0 || MaximumSessionSegmentsPerPosition<=0 || SchedulingTimeIncrementMinutes<=0 || SolverTimeLimitSeconds<=0 || FairnessWeight<0 || DailyBalanceWeight<0 || DutyCountBalanceWeight<0 || UnassignedTeacherPenalty<0 || string.IsNullOrWhiteSpace(SchoolName))
            throw new ArgumentException("Scheduling limits, weights, and school name are invalid.");
    }
}
