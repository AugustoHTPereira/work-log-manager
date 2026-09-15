using WorkLogManager.Application.Common;
using WorkLogManager.Application.Tests.TestHelpers;

namespace WorkLogManager.Application.Tests.Entities;

public class EmployeeWorkLogTests
{
    [Fact]
    public void CalculateDuration_ValidDates_SetsDurationSeconds()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var workLog = EntityFactory.CreateEmployeeWorkLog(startDate: start, endDate: end);

        Assert.Equal(5400, workLog.DurationSeconds);
    }

    [Fact]
    public void CalculateDuration_EndDateBeforeStartDate_ThrowsDomainException()
    {
        var start = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var workLog = EntityFactory.CreateEmployeeWorkLog(startDate: start, endDate: start.AddHours(1));

        workLog.EndDate = start.AddHours(-1);

        Assert.Throws<DomainException>(() => workLog.CalculateDuration());
    }

    [Fact]
    public void CalculateDuration_CalledAgainAfterDatesChange_RecalculatesDuration()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var workLog = EntityFactory.CreateEmployeeWorkLog(startDate: start, endDate: start.AddHours(1));

        workLog.EndDate = start.AddHours(2);
        workLog.CalculateDuration();

        Assert.Equal(7200, workLog.DurationSeconds);
    }

    [Fact]
    public void Touch_UpdatesUpdatedAtUtc()
    {
        var workLog = EntityFactory.CreateEmployeeWorkLog();
        var originalUpdatedAt = workLog.UpdatedAtUtc;

        workLog.Touch();

        Assert.True(workLog.UpdatedAtUtc >= originalUpdatedAt);
    }
}
