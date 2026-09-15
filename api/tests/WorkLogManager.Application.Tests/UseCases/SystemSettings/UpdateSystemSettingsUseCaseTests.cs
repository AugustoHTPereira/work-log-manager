using FluentValidation;
using Moq;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Tests.TestHelpers;
using WorkLogManager.Application.UseCases.SystemSettings;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.UseCases.SystemSettings;

public class UpdateSystemSettingsUseCaseTests
{
    private static UpdateSystemSettingsUseCase CreateUseCase(Mock<ISystemSettingsRepository> repository)
    {
        var getSystemSettingsUseCase = new GetSystemSettingsUseCase(repository.Object);
        return new UpdateSystemSettingsUseCase(repository.Object, getSystemSettingsUseCase, new SystemSettingsValidator());
    }

    [Fact]
    public async Task ExecuteAsync_ValidData_UpdatesExistingSettingsAndTouchesTimestamp()
    {
        var existingSettings = EntityFactory.CreateSystemSettings(8m);
        var originalUpdatedAt = existingSettings.UpdatedAtUtc;

        var repository = new Mock<ISystemSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existingSettings);

        var useCase = CreateUseCase(repository);
        var input = EntityFactory.CreateSystemSettings(6m);

        var result = await useCase.ExecuteAsync(input);

        Assert.Same(existingSettings, result);
        Assert.Equal(6m, result.DefaultDailyWorkHours);
        Assert.True(result.UpdatedAtUtc >= originalUpdatedAt);
        repository.Verify(r => r.UpsertAsync(existingSettings, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoExistingSettingsYet_CreatesDefaultThenAppliesUpdate()
    {
        var repository = new Mock<ISystemSettingsRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Application.Entities.SystemSettings?)null);

        var useCase = CreateUseCase(repository);
        var input = EntityFactory.CreateSystemSettings(10m);

        var result = await useCase.ExecuteAsync(input);

        Assert.Equal(10m, result.DefaultDailyWorkHours);
        repository.Verify(r => r.UpsertAsync(It.IsAny<Application.Entities.SystemSettings>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ExecuteAsync_NonPositiveDailyWorkHours_ThrowsValidationExceptionAndDoesNotUpsert(decimal defaultDailyWorkHours)
    {
        var repository = new Mock<ISystemSettingsRepository>();

        var useCase = CreateUseCase(repository);
        var input = EntityFactory.CreateSystemSettings(defaultDailyWorkHours);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(input));

        repository.Verify(r => r.UpsertAsync(It.IsAny<Application.Entities.SystemSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
