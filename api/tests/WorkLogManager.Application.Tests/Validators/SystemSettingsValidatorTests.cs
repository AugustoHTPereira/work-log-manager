using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.Validators;

public class SystemSettingsValidatorTests
{
    private readonly SystemSettingsValidator _validator = new();

    [Fact]
    public void Validate_PositiveDailyWorkHours_HasNoErrors()
    {
        var settings = EntityFactory.CreateSystemSettings(8m);

        var result = _validator.Validate(settings);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveDailyWorkHours_HasError(decimal defaultDailyWorkHours)
    {
        var settings = EntityFactory.CreateSystemSettings(defaultDailyWorkHours);

        var result = _validator.Validate(settings);

        Assert.False(result.IsValid);
    }
}
