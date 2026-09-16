using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.Validators;

public class WorkSchedulePeriodValidatorTests
{
    private readonly WorkSchedulePeriodValidator _validator = new();

    [Fact]
    public void Validate_ValidPeriod_HasNoErrors()
    {
        var period = EntityFactory.CreateWorkSchedulePeriod();

        var result = _validator.Validate(period);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EndTimeEqualToStartTime_HasError()
    {
        var period = EntityFactory.CreateWorkSchedulePeriod(startTime: new TimeOnly(8, 0), endTime: new TimeOnly(8, 0));

        var result = _validator.Validate(period);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_EndTimeBeforeStartTime_HasError()
    {
        var period = EntityFactory.CreateWorkSchedulePeriod(startTime: new TimeOnly(12, 0), endTime: new TimeOnly(8, 0));

        var result = _validator.Validate(period);

        Assert.False(result.IsValid);
    }
}
