using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.Tests.Services;

public class AllowManageClosedWorkLogsParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-bool")]
    public void Parse_NullEmptyOrInvalid_ReturnsFalse(string? rawValue)
    {
        Assert.False(AllowManageClosedWorkLogsParser.Parse(rawValue));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    public void Parse_TrueVariants_ReturnsTrue(string rawValue)
    {
        Assert.True(AllowManageClosedWorkLogsParser.Parse(rawValue));
    }

    [Fact]
    public void Parse_False_ReturnsFalse()
    {
        Assert.False(AllowManageClosedWorkLogsParser.Parse("false"));
    }
}
