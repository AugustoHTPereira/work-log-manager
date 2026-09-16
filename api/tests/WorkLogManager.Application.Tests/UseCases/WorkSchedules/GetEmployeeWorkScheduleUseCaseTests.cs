using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.WorkSchedules;

namespace WorkLogManager.Application.Tests.UseCases.WorkSchedules;

public class GetEmployeeWorkScheduleUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_EmployeeExists_ReturnsEmployeePeriods()
    {
        var employee = EntityFactory.CreateEmployee();

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var periods = new List<WorkSchedulePeriod> { EntityFactory.CreateWorkSchedulePeriod(employeeId: employee.Id) };
        var workSchedulePeriodRepository = new Mock<IWorkSchedulePeriodRepository>();
        workSchedulePeriodRepository.Setup(r => r.ListAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(periods);

        var useCase = new GetEmployeeWorkScheduleUseCase(employeeRepository.Object, workSchedulePeriodRepository.Object);

        var result = await useCase.ExecuteAsync(employee.Id);

        Assert.Same(periods, result);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeDoesNotExist_ThrowsNotFoundException()
    {
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);

        var workSchedulePeriodRepository = new Mock<IWorkSchedulePeriodRepository>();

        var useCase = new GetEmployeeWorkScheduleUseCase(employeeRepository.Object, workSchedulePeriodRepository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
    }
}
