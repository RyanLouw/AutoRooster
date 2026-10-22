namespace SchoolExamScheduler.Domain;

public readonly record struct TimeInterval
{
    public TimeOnly Start { get; }
    public TimeOnly End { get; }
    public int Minutes => (int)(End.ToTimeSpan() - Start.ToTimeSpan()).TotalMinutes;
    public TimeInterval(TimeOnly start, TimeOnly end)
    {
        if (start >= end || start.Second != 0 || end.Second != 0 || start.Ticks % TimeSpan.TicksPerMinute != 0 || end.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new ArgumentException("Times must use whole minutes, start before end, and remain on one date.");
        Start = start; End = end;
    }
    public bool Overlaps(TimeInterval other) => Start < other.End && other.Start < End;
    public bool Contains(TimeInterval other) => Start <= other.Start && End >= other.End;
}

internal static class Guard
{
    internal static string Text(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.") : value.Trim();
    internal static int Positive(int value, string name) => value > 0 ? value : throw new ArgumentException($"{name} must be positive.");
    internal static int? Limit(int? value) => value is <= 0 ? throw new ArgumentException("Workload limits must be positive or empty.") : value;
    internal static Guid Id(Guid value) => value != Guid.Empty ? value : throw new ArgumentException("An identifier is required.");
}
