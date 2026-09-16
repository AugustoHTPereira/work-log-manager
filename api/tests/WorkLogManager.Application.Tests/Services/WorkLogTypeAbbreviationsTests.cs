using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Services;

namespace WorkLogManager.Application.Tests.Services;

public class WorkLogTypeAbbreviationsTests
{
    [Theory]
    [InlineData(WorkLogType.RegularAttendance, "PR")]
    [InlineData(WorkLogType.Overtime, "HE")]
    [InlineData(WorkLogType.Absence, "FALTA")]
    [InlineData(WorkLogType.Break, "INT")]
    public void Get_KnownWorkLogType_ReturnsExpectedAbbreviation(WorkLogType type, string expected)
    {
        var abbreviation = WorkLogTypeAbbreviations.Get(type);

        Assert.Equal(expected, abbreviation);
    }
}
