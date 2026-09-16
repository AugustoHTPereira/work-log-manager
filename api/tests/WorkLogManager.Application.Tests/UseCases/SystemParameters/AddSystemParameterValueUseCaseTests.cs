using FluentValidation;
using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.UseCases.SystemParameters;
using WorkLogManager.Application.Validators;

namespace WorkLogManager.Application.Tests.UseCases.SystemParameters;

public class AddSystemParameterValueUseCaseTests
{
    private static AddSystemParameterValueUseCase CreateUseCase(Mock<ISystemParameterRepository> repository)
    {
        return new AddSystemParameterValueUseCase(repository.Object, new SystemParameterValidator());
    }

    [Fact]
    public async Task ExecuteAsync_NewValue_InsertsRow()
    {
        var repository = new Mock<ISystemParameterRepository>();
        repository
            .Setup(r => r.ListByParamAsync(SystemParameterName.AutoWorkLogTypes, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)[]);

        var useCase = CreateUseCase(repository);

        var result = await useCase.ExecuteAsync(SystemParameterName.AutoWorkLogTypes, "Break");

        Assert.Equal("Break", result.Value);
        Assert.Equal(SystemParameterValueType.Array, result.ValueType);
        repository.Verify(r => r.AddAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ValueAlreadyPresent_ReturnsExistingRowAndDoesNotInsert()
    {
        var existing = new SystemParameter
        {
            Id = Guid.NewGuid(),
            Param = SystemParameterName.AutoWorkLogTypes,
            Value = "Break",
            ValueType = SystemParameterValueType.Array,
        };

        var repository = new Mock<ISystemParameterRepository>();
        repository
            .Setup(r => r.ListByParamAsync(SystemParameterName.AutoWorkLogTypes, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)[existing]);

        var useCase = CreateUseCase(repository);

        var result = await useCase.ExecuteAsync(SystemParameterName.AutoWorkLogTypes, "Break");

        Assert.Same(existing, result);
        repository.Verify(r => r.AddAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidValue_ThrowsValidationExceptionAndDoesNotPersist()
    {
        var repository = new Mock<ISystemParameterRepository>();
        repository
            .Setup(r => r.ListByParamAsync(SystemParameterName.AutoWorkLogTypes, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)[]);

        var useCase = CreateUseCase(repository);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(SystemParameterName.AutoWorkLogTypes, "Overtime"));

        repository.Verify(r => r.AddAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ScalarParam_ThrowsDomainExceptionAndDoesNotPersist()
    {
        var repository = new Mock<ISystemParameterRepository>();

        var useCase = CreateUseCase(repository);

        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(SystemParameterName.AllowManageClosedWorkLogs, "true"));

        repository.Verify(r => r.AddAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
