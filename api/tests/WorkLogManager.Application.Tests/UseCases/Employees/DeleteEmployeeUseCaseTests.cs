using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.Employees;

namespace WorkLogManager.Application.Tests.UseCases.Employees;

public class DeleteEmployeeUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ExistingEmployee_DeletesEmployee()
    {
        var employee = EntityFactory.CreateEmployee();

        var repository = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var useCase = new DeleteEmployeeUseCase(repository.Object);

        await useCase.ExecuteAsync(employee.Id);

        repository.Verify(r => r.DeleteAsync(employee, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistingEmployee_ThrowsNotFoundException()
    {
        var repository = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);

        var useCase = new DeleteEmployeeUseCase(repository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
    }
}
