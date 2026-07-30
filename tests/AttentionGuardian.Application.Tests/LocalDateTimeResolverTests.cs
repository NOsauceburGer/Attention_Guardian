using AttentionGuardian.Application;

namespace AttentionGuardian.Application.Tests;

public sealed class LocalDateTimeResolverTests
{
    private static readonly TimeZoneInfo TestTimeZone = CreateTestTimeZone();

    [Fact]
    public void Resolve_NormalDaylightTime_ReturnsExpectedOffset()
    {
        var result = LocalDateTimeResolver.Resolve(
            new DateTime(2026, 7, 25, 18, 0, 0),
            TestTimeZone);

        Assert.Equal(TimeSpan.FromHours(-7), result.Offset);
    }

    [Fact]
    public void Resolve_InvalidSpringForwardTime_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            LocalDateTimeResolver.Resolve(
                new DateTime(2026, 3, 8, 2, 30, 0),
                TestTimeZone));

        Assert.Contains("does not exist", exception.Message);
    }

    [Fact]
    public void Resolve_AmbiguousFallBackTime_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            LocalDateTimeResolver.Resolve(
                new DateTime(2026, 11, 1, 1, 30, 0),
                TestTimeZone));

        Assert.Contains("occurs twice", exception.Message);
    }

    private static TimeZoneInfo CreateTestTimeZone()
    {
        var daylightStart = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            month: 3,
            day: 8);
        var daylightEnd = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            month: 11,
            day: 1);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2020, 1, 1),
            new DateTime(2030, 12, 31),
            TimeSpan.FromHours(1),
            daylightStart,
            daylightEnd);

        return TimeZoneInfo.CreateCustomTimeZone(
            "AttentionGuardian.TestTimeZone",
            TimeSpan.FromHours(-8),
            "Attention Guardian Test Time Zone",
            "Test Standard Time",
            "Test Daylight Time",
            [rule]);
    }
}
