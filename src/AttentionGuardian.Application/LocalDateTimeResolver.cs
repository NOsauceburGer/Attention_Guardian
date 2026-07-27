namespace AttentionGuardian.Application;

public static class LocalDateTimeResolver
{
    public static DateTimeOffset Resolve(DateTime localDateTime, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(unspecified))
        {
            throw new ArgumentException("This local time does not exist in the selected time zone.");
        }

        if (timeZone.IsAmbiguousTime(unspecified))
        {
            throw new ArgumentException("This local time occurs twice in the selected time zone.");
        }

        return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified));
    }
}
