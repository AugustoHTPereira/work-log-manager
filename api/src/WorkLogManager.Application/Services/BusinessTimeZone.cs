namespace WorkLogManager.Application.Services;

/// <summary>
/// Single, centralized definition of the business time zone (America/Sao_Paulo) used to
/// interpret every "local" business hour (e.g. workday start, break window) referenced by
/// the application, and to convert between that local time and the UTC instants persisted
/// and exchanged across the system. This is the only place in the codebase that references
/// the IANA time zone identifier.
/// </summary>
public static class BusinessTimeZone
{
    /// <summary>
    /// The business time zone: America/Sao_Paulo (UTC-3, fixed, no daylight saving time
    /// observed since 2019).
    /// </summary>
    public static TimeZoneInfo Value { get; } = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>
    /// Combines a calendar date and a "business local" time of day and returns the
    /// corresponding UTC instant, resolving the correct offset for that date/time in the
    /// business time zone.
    /// </summary>
    public static DateTimeOffset ToInstant(DateOnly date, TimeOnly time)
    {
        return ToInstant(date.ToDateTime(time));
    }

    /// <summary>
    /// Interprets the given <see cref="DateTime"/> as "business local" time and returns the
    /// corresponding UTC instant, resolving the correct offset for that date/time in the
    /// business time zone.
    /// </summary>
    public static DateTimeOffset ToInstant(DateTime localDateTime)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        var offset = Value.GetUtcOffset(unspecified);
        var local = new DateTimeOffset(unspecified, offset);
        return local.ToUniversalTime();
    }

    /// <summary>
    /// Converts a UTC instant to the corresponding calendar date in the business time zone
    /// (e.g. used to classify which "business day" a persisted <see cref="DateTimeOffset"/>
    /// belongs to).
    /// </summary>
    public static DateOnly ToBusinessDate(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, Value);
        return DateOnly.FromDateTime(local.DateTime);
    }

    /// <summary>
    /// Converts a UTC instant to the corresponding "wall clock" <see cref="DateTime"/> in the
    /// business time zone (e.g. used to display the local time of day of a persisted
    /// <see cref="DateTimeOffset"/>, such as in the month closing report's schedule column).
    /// </summary>
    public static DateTime ToBusinessDateTime(DateTimeOffset instant)
    {
        return TimeZoneInfo.ConvertTime(instant, Value).DateTime;
    }
}
