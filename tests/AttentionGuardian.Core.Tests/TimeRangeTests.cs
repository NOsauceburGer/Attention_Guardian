using AttentionGuardian.Core;

namespace AttentionGuardian.Core.Tests;

public sealed class TimeRangeTests
{
    [Fact]
    public void Contains_UsesInclusiveStartAndExclusiveEnd()
    {
        var range = Range(9, 0, 10, 0);

        Assert.True(range.Contains(At(9, 0)));
        Assert.True(range.Contains(At(9, 59)));
        Assert.False(range.Contains(At(10, 0)));
    }

    [Fact]
    public void Overlaps_TouchingBoundariesDoNotOverlap()
    {
        var first = Range(9, 0, 10, 0);
        var second = Range(10, 0, 11, 0);

        Assert.False(first.Overlaps(second));
        Assert.False(second.Overlaps(first));
    }

    [Fact]
    public void Overlaps_SharedTimeIsDetectedSymmetrically()
    {
        var first = Range(9, 0, 10, 0);
        var second = Range(9, 30, 10, 30);

        Assert.True(first.Overlaps(second));
        Assert.True(second.Overlaps(first));
    }

    [Fact]
    public void Constructor_RejectsZeroOrNegativeDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TimeRange(At(9, 0), At(9, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TimeRange(At(10, 0), At(9, 0)));
    }

    private static TimeRange Range(int startHour, int startMinute, int endHour, int endMinute) =>
        new(At(startHour, startMinute), At(endHour, endMinute));

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 7, 26, hour, minute, 0, TimeSpan.FromHours(8));
}
