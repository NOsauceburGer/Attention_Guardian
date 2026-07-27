using AttentionGuardian.Core;

namespace AttentionGuardian.Core.Tests;

public sealed class FixedEventTests
{
    [Fact]
    public void HandoffTime_SubtractsAllLeadDurations()
    {
        var fixedEvent = CreateEvent(
            new DateTimeOffset(2026, 7, 25, 18, 0, 0, TimeSpan.FromHours(8)),
            preparationMinutes: 20,
            travelMinutes: 30,
            bufferMinutes: 10);

        Assert.Equal(
            new DateTimeOffset(2026, 7, 25, 17, 0, 0, TimeSpan.FromHours(8)),
            fixedEvent.HandoffTime);
    }

    [Fact]
    public void HandoffTime_CanCrossMidnight()
    {
        var fixedEvent = CreateEvent(
            new DateTimeOffset(2026, 7, 26, 0, 30, 0, TimeSpan.FromHours(8)),
            preparationMinutes: 20,
            travelMinutes: 30,
            bufferMinutes: 10);

        Assert.Equal(
            new DateTimeOffset(2026, 7, 25, 23, 30, 0, TimeSpan.FromHours(8)),
            fixedEvent.HandoffTime);
    }

    [Fact]
    public void HandoffTime_WithZeroDurations_EqualsStartTime()
    {
        var startTime = new DateTimeOffset(2026, 7, 25, 18, 0, 0, TimeSpan.FromHours(8));
        var fixedEvent = CreateEvent(startTime, 0, 0, 0);

        Assert.Equal(startTime, fixedEvent.HandoffTime);
    }

    [Theory]
    [InlineData(-1, 0, 0, "preparationDuration")]
    [InlineData(0, -1, 0, "travelDuration")]
    [InlineData(0, 0, -1, "safetyBuffer")]
    public void Constructor_RejectsNegativeDurations(
        int preparationMinutes,
        int travelMinutes,
        int bufferMinutes,
        string expectedParameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateEvent(
                new DateTimeOffset(2026, 7, 25, 18, 0, 0, TimeSpan.FromHours(8)),
                preparationMinutes,
                travelMinutes,
                bufferMinutes));

        Assert.Equal(expectedParameter, exception.ParamName);
    }

    [Fact]
    public void Constructor_RejectsHandoffTimeOutsideSupportedRange()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FixedEvent(
                DateTimeOffset.MinValue,
                TimeSpan.FromMinutes(1),
                TimeSpan.Zero,
                TimeSpan.Zero));

        Assert.Equal("StartTime", exception.ParamName);
    }

    private static FixedEvent CreateEvent(
        DateTimeOffset startTime,
        int preparationMinutes,
        int travelMinutes,
        int bufferMinutes) =>
        new(
            startTime,
            TimeSpan.FromMinutes(preparationMinutes),
            TimeSpan.FromMinutes(travelMinutes),
            TimeSpan.FromMinutes(bufferMinutes));
}
