using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.Tests.Services;

public class BusinessTimeZoneTests
{
    [Fact]
    public void ToInstant_GivenBusinessLocalDateAndTime_ReturnsUtcNormalizedInstantRepresentingTheCorrectLocalTime()
    {
        var instant = BusinessTimeZone.ToInstant(new DateOnly(2026, 1, 5), new TimeOnly(7, 0));

        Assert.Equal(TimeSpan.Zero, instant.Offset);

        var local = TimeZoneInfo.ConvertTime(instant, BusinessTimeZone.Value);
        Assert.Equal(7, local.Hour);
    }

    [Fact]
    public void ToBusinessDate_IsInverseOfToInstant_ForTheSameDateAndTime()
    {
        var date = new DateOnly(2026, 1, 5);
        var time = new TimeOnly(7, 0);

        var instant = BusinessTimeZone.ToInstant(date, time);
        var roundTrippedDate = BusinessTimeZone.ToBusinessDate(instant);

        Assert.Equal(date, roundTrippedDate);
    }

    [Fact]
    public void ToBusinessDateTime_GivenUtcInstant_ReturnsLocalWallClockTime()
    {
        var instant = new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero);

        var local = BusinessTimeZone.ToBusinessDateTime(instant);

        Assert.Equal(new DateTime(2026, 1, 5, 7, 0, 0), local);
    }

    [Fact]
    public void ToBusinessDateTime_IsInverseOfToInstant_ForTheSameDateAndTime()
    {
        var date = new DateOnly(2026, 1, 5);
        var time = new TimeOnly(7, 0);

        var instant = BusinessTimeZone.ToInstant(date, time);
        var roundTripped = BusinessTimeZone.ToBusinessDateTime(instant);

        Assert.Equal(date.ToDateTime(time), roundTripped);
    }
}
