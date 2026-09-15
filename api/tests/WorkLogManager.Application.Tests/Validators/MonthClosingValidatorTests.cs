using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.Validators;

public class MonthClosingValidatorTests
{
    private readonly MonthClosingValidator _validator = new();

    [Fact]
    public void Validate_PastMonth_HasNoErrors()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 1, year: 2020);

        var result = _validator.Validate(monthClosing);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_CurrentMonth_HasError()
    {
        var now = DateTimeOffset.UtcNow;
        var monthClosing = EntityFactory.CreateMonthClosing(month: now.Month, year: now.Year);

        var result = _validator.Validate(monthClosing);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_FutureMonth_HasError()
    {
        var future = DateTimeOffset.UtcNow.AddYears(1);
        var monthClosing = EntityFactory.CreateMonthClosing(month: future.Month, year: future.Year);

        var result = _validator.Validate(monthClosing);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Validate_MonthOutOfRange_HasError(int month)
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: month, year: 2020);

        var result = _validator.Validate(monthClosing);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_YearNotPositive_HasError()
    {
        var monthClosing = EntityFactory.CreateMonthClosing(month: 1, year: 0);

        var result = _validator.Validate(monthClosing);

        Assert.False(result.IsValid);
    }
}
