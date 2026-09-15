using WorkLogManager.Application.Common;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.Tests.Services;

public class WorkLogDurationParserTests
{
    [Fact]
    public void Parse_CombinedHoursAndMinutes_ReturnsTotalSeconds()
    {
        var result = WorkLogDurationParser.Parse("1h 30m");

        Assert.Equal(5400, result);
    }

    [Fact]
    public void Parse_TwoMonths_ReturnsTotalSeconds()
    {
        var result = WorkLogDurationParser.Parse("2M");

        Assert.Equal(2 * 30 * 86400, result);
    }

    [Fact]
    public void Parse_OneYear_ReturnsTotalSeconds()
    {
        var result = WorkLogDurationParser.Parse("1A");

        Assert.Equal(365 * 86400, result);
    }

    [Fact]
    public void Parse_OneWeek_ReturnsTotalSeconds()
    {
        var result = WorkLogDurationParser.Parse("1S");

        Assert.Equal(7 * 86400, result);
    }

    [Fact]
    public void Parse_InvalidToken_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => WorkLogDurationParser.Parse("abc"));
    }

    [Fact]
    public void Parse_UnknownUnit_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => WorkLogDurationParser.Parse("1x"));
    }

    [Fact]
    public void Parse_WrongCaseUnit_ThrowsDomainException()
    {
        // "H" (uppercase) is not a valid unit; only lowercase "h" is hour.
        Assert.Throws<DomainException>(() => WorkLogDurationParser.Parse("1H"));
    }

    [Theory]
    [InlineData(5400, "1h 30m")]
    [InlineData(90061, "1d 1h 1m 1s")]
    [InlineData(0, "0s")]
    public void Format_ReturnsExpectedText(long totalSeconds, string expected)
    {
        var result = WorkLogDurationParser.Format(totalSeconds);

        Assert.Equal(expected, result);
    }
}
