using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Services;
using WorkLogManager.Application.Tests.TestHelpers;

namespace WorkLogManager.Application.Tests.Services;

public class WorkScheduleOverlapCheckerTests
{
    [Fact]
    public void EnsureNoOverlap_OverlappingPeriodsSameDay_ThrowsDomainException()
    {
        var periods = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(11, 0), endTime: new TimeOnly(15, 0)),
        };

        Assert.Throws<DomainException>(() => WorkScheduleOverlapChecker.EnsureNoOverlap(periods));
    }

    [Fact]
    public void EnsureNoOverlap_NonOverlappingPeriodsSameDay_DoesNotThrow()
    {
        var periods = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(13, 0), endTime: new TimeOnly(17, 0)),
        };

        WorkScheduleOverlapChecker.EnsureNoOverlap(periods);
    }

    [Fact]
    public void EnsureNoOverlap_OverlappingTimesDifferentDays_DoesNotThrow()
    {
        var periods = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Tuesday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
        };

        WorkScheduleOverlapChecker.EnsureNoOverlap(periods);
    }

    [Fact]
    public void EnsureNoOverlap_AdjacentPeriods_DoesNotThrow()
    {
        var periods = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(12, 0), endTime: new TimeOnly(17, 0)),
        };

        WorkScheduleOverlapChecker.EnsureNoOverlap(periods);
    }
}
