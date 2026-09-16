using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Services;
using WorkLogManager.Application.Tests.TestHelpers;

namespace WorkLogManager.Application.Tests.Services;

public class WorkScheduleResolverTests
{
    [Fact]
    public void GetEffectivePeriods_EmployeeHasPeriodForDay_IgnoresGeneralConfiguration()
    {
        var generalPeriods = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
        };
        var employeePeriods = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(9, 0), endTime: new TimeOnly(11, 0)),
        };

        var result = WorkScheduleResolver.GetEffectivePeriods(DayOfWeek.Monday, generalPeriods, employeePeriods);

        Assert.Single(result);
        Assert.Equal(employeePeriods[0].Id, result[0].Id);
    }

    [Fact]
    public void GetEffectivePeriods_EmployeeHasNoPeriodForDay_FallsBackToGeneral()
    {
        var generalPeriods = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
        };
        var employeePeriods = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Tuesday, startTime: new TimeOnly(9, 0), endTime: new TimeOnly(11, 0)),
        };

        var result = WorkScheduleResolver.GetEffectivePeriods(DayOfWeek.Monday, generalPeriods, employeePeriods);

        Assert.Single(result);
        Assert.Equal(generalPeriods[0].Id, result[0].Id);
    }

    [Fact]
    public void GetEffectivePeriods_NeitherHasPeriodForDay_ReturnsEmpty()
    {
        var generalPeriods = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Tuesday),
        };
        var employeePeriods = new List<WorkSchedulePeriod>();

        var result = WorkScheduleResolver.GetEffectivePeriods(DayOfWeek.Monday, generalPeriods, employeePeriods);

        Assert.Empty(result);
    }

    [Fact]
    public void GetEffectiveDailyWorkHours_NeitherHasPeriodForDay_ReturnsZero()
    {
        var hours = WorkScheduleResolver.GetEffectiveDailyWorkHours(DayOfWeek.Sunday, [], []);

        Assert.Equal(0m, hours);
    }

    [Fact]
    public void GetEffectiveDailyWorkHours_MultiplePeriods_SumsTheirDurations()
    {
        var generalPeriods = new List<WorkSchedulePeriod>
        {
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(8, 0), endTime: new TimeOnly(12, 0)),
            EntityFactory.CreateWorkSchedulePeriod(dayOfWeek: DayOfWeek.Monday, startTime: new TimeOnly(13, 0), endTime: new TimeOnly(17, 0)),
        };

        var hours = WorkScheduleResolver.GetEffectiveDailyWorkHours(DayOfWeek.Monday, generalPeriods, []);

        Assert.Equal(8m, hours);
    }
}
