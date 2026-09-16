using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.Tests.Services;

public class AutoWorkLogTypesParserTests
{
    private static SystemParameter BuildRow(WorkLogType type) => new()
    {
        Id = Guid.NewGuid(),
        Param = SystemParameterName.AutoWorkLogTypes,
        Value = type.ToString(),
        ValueType = SystemParameterValueType.Array,
    };

    [Fact]
    public void Parse_NoRows_ReturnsOnlyRegularAttendance()
    {
        var result = AutoWorkLogTypesParser.Parse([]);

        Assert.Equal(new HashSet<WorkLogType> { WorkLogType.RegularAttendance }, result);
    }

    [Fact]
    public void Parse_RegularAttendanceAndBreakRows_ReturnsBoth()
    {
        var result = AutoWorkLogTypesParser.Parse([BuildRow(WorkLogType.RegularAttendance), BuildRow(WorkLogType.Break)]);

        Assert.Equal(new HashSet<WorkLogType> { WorkLogType.RegularAttendance, WorkLogType.Break }, result);
    }

    [Fact]
    public void Parse_OnlyBreakRow_ReturnsOnlyBreak()
    {
        var result = AutoWorkLogTypesParser.Parse([BuildRow(WorkLogType.Break)]);

        Assert.Equal(new HashSet<WorkLogType> { WorkLogType.Break }, result);
    }
}
