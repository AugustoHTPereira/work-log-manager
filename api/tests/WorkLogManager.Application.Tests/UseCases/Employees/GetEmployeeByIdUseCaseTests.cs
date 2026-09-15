using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.Employees;
using WorkLogManager.Application.UseCases.SystemSettings;

namespace WorkLogManager.Application.Tests.UseCases.Employees;

public class GetEmployeeByIdUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_EmployeeHasDailyWorkHours_ReturnsEmployeeSpecificValue()
    {
        var employee = EntityFactory.CreateEmployee(dailyWorkHours: 6m);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        workLogRepository.Setup(r => r.ListByEmployeeIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeWorkLog>());

        var systemSettingsRepository = new Mock<ISystemSettingsRepository>();
        systemSettingsRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntityFactory.CreateSystemSettings(8m));

        var getSystemSettingsUseCase = new GetSystemSettingsUseCase(systemSettingsRepository.Object);
        var useCase = new GetEmployeeByIdUseCase(employeeRepository.Object, workLogRepository.Object, getSystemSettingsUseCase);

        var result = await useCase.ExecuteAsync(employee.Id);

        Assert.Equal(6m, result.EffectiveDailyWorkHours);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeHasNoDailyWorkHours_ReturnsSystemDefault()
    {
        var employee = EntityFactory.CreateEmployee(dailyWorkHours: null);

        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        workLogRepository.Setup(r => r.ListByEmployeeIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeWorkLog>());

        var systemSettingsRepository = new Mock<ISystemSettingsRepository>();
        systemSettingsRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntityFactory.CreateSystemSettings(8m));

        var getSystemSettingsUseCase = new GetSystemSettingsUseCase(systemSettingsRepository.Object);
        var useCase = new GetEmployeeByIdUseCase(employeeRepository.Object, workLogRepository.Object, getSystemSettingsUseCase);

        var result = await useCase.ExecuteAsync(employee.Id);

        Assert.Equal(8m, result.EffectiveDailyWorkHours);
    }

    [Fact]
    public async Task ExecuteAsync_EmployeeDoesNotExist_ThrowsNotFoundException()
    {
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var workLogRepository = new Mock<IEmployeeWorkLogRepository>();
        var systemSettingsRepository = new Mock<ISystemSettingsRepository>();
        var getSystemSettingsUseCase = new GetSystemSettingsUseCase(systemSettingsRepository.Object);

        var useCase = new GetEmployeeByIdUseCase(employeeRepository.Object, workLogRepository.Object, getSystemSettingsUseCase);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
    }
}
