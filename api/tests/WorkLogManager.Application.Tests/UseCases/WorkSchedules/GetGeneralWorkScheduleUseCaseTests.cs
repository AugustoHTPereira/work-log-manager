using Moq;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.WorkSchedules;

namespace WorkLogManager.Application.Tests.UseCases.WorkSchedules;

public class GetGeneralWorkScheduleUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsGeneralPeriodsFromRepository()
    {
        var repository = new Mock<IWorkSchedulePeriodRepository>();
        var periods = new List<WorkSchedulePeriod> { EntityFactory.CreateWorkSchedulePeriod() };
        repository.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(periods);

        var useCase = new GetGeneralWorkScheduleUseCase(repository.Object);

        var result = await useCase.ExecuteAsync();

        Assert.Same(periods, result);
        repository.Verify(r => r.ListAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
