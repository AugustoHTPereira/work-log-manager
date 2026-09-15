using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.Validators;

public class EmployeeWorkLogValidatorTests
{
    private readonly EmployeeWorkLogValidator _validator = new();

    [Fact]
    public void Validate_EndDateAfterStartDate_HasNoErrors()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var workLog = EntityFactory.CreateEmployeeWorkLog(startDate: start, endDate: start.AddHours(1));

        var result = _validator.Validate(workLog);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EndDateEqualsStartDate_HasNoErrors()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var workLog = EntityFactory.CreateEmployeeWorkLog(startDate: start, endDate: start);

        var result = _validator.Validate(workLog);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EndDateBeforeStartDate_HasError()
    {
        var start = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var workLog = EntityFactory.CreateEmployeeWorkLog(startDate: start, endDate: start.AddHours(1));
        workLog.EndDate = start.AddHours(-1);

        var result = _validator.Validate(workLog);

        Assert.False(result.IsValid);
    }
}
