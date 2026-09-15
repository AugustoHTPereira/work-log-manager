using WorkLogManager.Application.Tests.TestHelpers;

namespace WorkLogManager.Application.Tests.Entities;

public class EmployeeTests
{
    [Fact]
    public void Touch_UpdatesUpdatedAtUtc()
    {
        var employee = EntityFactory.CreateEmployee();
        var originalUpdatedAt = employee.UpdatedAtUtc;

        employee.Touch();

        Assert.True(employee.UpdatedAtUtc >= originalUpdatedAt);
    }
}
