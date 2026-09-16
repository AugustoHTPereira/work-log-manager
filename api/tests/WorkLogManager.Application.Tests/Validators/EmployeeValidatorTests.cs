using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.Validators;

public class EmployeeValidatorTests
{
    private readonly EmployeeValidator _validator = new();

    [Fact]
    public void Validate_ValidEmployee_HasNoErrors()
    {
        var employee = EntityFactory.CreateEmployee();

        var result = _validator.Validate(employee);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Developer")]
    [InlineData("Jane Doe", "")]
    public void Validate_MissingNameOrRole_HasErrors(string name, string role)
    {
        var employee = EntityFactory.CreateEmployee(name: name, role: role);

        var result = _validator.Validate(employee);

        Assert.False(result.IsValid);
    }
}
