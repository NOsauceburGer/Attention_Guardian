namespace AttentionGuardian.Core;

public readonly record struct TimeRange
{
    public TimeRange(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(
                nameof(end),
                end,
                "The end of a time range must be later than its start.");
        }

        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeSpan Duration => End - Start;

    public bool Contains(DateTimeOffset instant) =>
        instant >= Start && instant < End;

    public bool Overlaps(TimeRange other) =>
        Start < other.End && other.Start < End;

    public TimeRange MoveTo(DateTimeOffset newStart)
    {
        try
        {
            return new TimeRange(newStart, newStart + Duration);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newStart),
                newStart,
                "Moving the time range would exceed the supported date range.");
        }
    }
}
