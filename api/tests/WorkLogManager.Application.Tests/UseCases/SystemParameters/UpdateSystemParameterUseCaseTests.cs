using FluentValidation;
using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.UseCases.SystemParameters;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.UseCases.SystemParameters;

public class UpdateSystemParameterUseCaseTests
{
    private static UpdateSystemParameterUseCase CreateUseCase(Mock<ISystemParameterRepository> repository)
    {
        return new UpdateSystemParameterUseCase(repository.Object, new SystemParameterValidator());
    }

    [Fact]
    public async Task ExecuteAsync_FirstWrite_InsertsWithNewIdAndTimestamps()
    {
        var repository = new Mock<ISystemParameterRepository>();
        repository
            .Setup(r => r.ListByParamAsync(SystemParameterName.AllowManageClosedWorkLogs, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)[]);

        var useCase = CreateUseCase(repository);

        var result = await useCase.ExecuteAsync(SystemParameterName.AllowManageClosedWorkLogs, "true");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(result.CreatedAtUtc, result.UpdatedAtUtc);
        Assert.Equal(SystemParameterValueType.Bool, result.ValueType);
        repository.Verify(r => r.AddAsync(result, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.UpdateAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SecondWrite_PreservesIdAndCreatedAtButUpdatesValueAndUpdatedAt()
    {
        var existingId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
        var existing = new SystemParameter
        {
            Id = existingId,
            Param = SystemParameterName.AllowManageClosedWorkLogs,
            Value = "false",
            ValueType = SystemParameterValueType.Bool,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
        };

        var repository = new Mock<ISystemParameterRepository>();
        repository
            .Setup(r => r.ListByParamAsync(SystemParameterName.AllowManageClosedWorkLogs, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)[existing]);

        var useCase = CreateUseCase(repository);

        var result = await useCase.ExecuteAsync(SystemParameterName.AllowManageClosedWorkLogs, "true");

        Assert.Equal(existingId, result.Id);
        Assert.Equal(createdAt, result.CreatedAtUtc);
        Assert.Equal("true", result.Value);
        Assert.True(result.UpdatedAtUtc > createdAt);
        repository.Verify(r => r.UpdateAsync(result, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.AddAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidBoolValue_ThrowsValidationExceptionAndDoesNotPersist()
    {
        var repository = new Mock<ISystemParameterRepository>();
        repository
            .Setup(r => r.ListByParamAsync(SystemParameterName.AllowManageClosedWorkLogs, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)[]);

        var useCase = CreateUseCase(repository);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(SystemParameterName.AllowManageClosedWorkLogs, "talvez"));

        repository.Verify(r => r.AddAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.UpdateAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ArrayParam_ThrowsDomainExceptionAndDoesNotPersist()
    {
        var repository = new Mock<ISystemParameterRepository>();

        var useCase = CreateUseCase(repository);

        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(SystemParameterName.AutoWorkLogTypes, "RegularAttendance"));

        repository.Verify(r => r.AddAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.UpdateAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
