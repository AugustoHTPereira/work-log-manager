using Moq;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.SystemSettings;

namespace WorkLogManager.Application.Tests.UseCases.SystemSettings;

public class GetSystemSettingsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_SettingsAlreadyExist_ReturnsExistingSettingsWithoutUpserting()
    {
        var existingSettings = EntityFactory.CreateSystemSettings(6.5m);

        var repository = new Mock<ISystemSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existingSettings);

        var useCase = new GetSystemSettingsUseCase(repository.Object);

        var result = await useCase.ExecuteAsync();

        Assert.Same(existingSettings, result);
        Assert.Equal(6.5m, result.DefaultDailyWorkHours);
        repository.Verify(r => r.UpsertAsync(It.IsAny<Application.Entities.SystemSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoSettingsYet_CreatesAndPersistsDefaultSettings()
    {
        var repository = new Mock<ISystemSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Application.Entities.SystemSettings?)null);

        var useCase = new GetSystemSettingsUseCase(repository.Object);

        var result = await useCase.ExecuteAsync();

        Assert.Equal(8m, result.DefaultDailyWorkHours);
        Assert.Equal(Application.Entities.SystemSettings.SingletonId, result.Id);
        repository.Verify(r => r.UpsertAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }
}
