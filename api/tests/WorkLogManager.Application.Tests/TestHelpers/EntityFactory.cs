using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Tests.TestHelpers;

/// <summary>
/// Builds fully-populated entity instances for tests. Entities expose <c>internal set</c>
/// properties (see the remarks on <see cref="Employee"/>), which this test project can use
/// directly thanks to <c>InternalsVisibleTo</c> declared in
/// <c>WorkLogManager.Application.csproj</c>.
/// </summary>
public static class EntityFactory
{
    public static Employee CreateEmployee(
        string name = "Jane Doe",
        string role = "Developer",
        DateOnly? hireDate = null,
        Guid? id = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Employee
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Role = role,
            HireDate = hireDate ?? new DateOnly(2020, 1, 1),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public static EmployeeWorkLog CreateEmployeeWorkLog(
        Guid? employeeId = null,
        WorkLogType type = WorkLogType.Overtime,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        Guid? id = null,
        string? note = null,
        WorkLogOrigin origin = WorkLogOrigin.Manual)
    {
        var start = startDate ?? new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var end = endDate ?? start.AddHours(1);

        var workLog = new EmployeeWorkLog
        {
            Id = id ?? Guid.NewGuid(),
            EmployeeId = employeeId ?? Guid.NewGuid(),
            Type = type,
            Origin = origin,
            StartDate = start,
            EndDate = end,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Note = note,
        };

        workLog.CalculateDuration();

        return workLog;
    }

    public static WorkSchedulePeriod CreateWorkSchedulePeriod(
        Guid? employeeId = null,
        DayOfWeek dayOfWeek = DayOfWeek.Monday,
        TimeOnly? startTime = null,
        TimeOnly? endTime = null,
        Guid? id = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkSchedulePeriod
        {
            Id = id ?? Guid.NewGuid(),
            EmployeeId = employeeId,
            DayOfWeek = dayOfWeek,
            StartTime = startTime ?? new TimeOnly(8, 0),
            EndTime = endTime ?? new TimeOnly(12, 0),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public static MonthClosing CreateMonthClosing(int month = 1, int year = 2026, Guid? id = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new MonthClosing
        {
            Id = id ?? Guid.NewGuid(),
            Month = month,
            Year = year,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
