using Moq;
using WorkLogManager.Application.Common;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.UseCases.SystemParameters;

namespace WorkLogManager.Application.Tests.UseCases.SystemParameters;

public class RemoveSystemParameterValueUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ExistingRowMatchingParam_DeletesRow()
    {
        var row = new SystemParameter
        {
            Id = Guid.NewGuid(),
            Param = SystemParameterName.AutoWorkLogTypes,
            Value = "Break",
            ValueType = SystemParameterValueType.Array,
        };

        var repository = new Mock<ISystemParameterRepository>();
        repository.Setup(r => r.GetByIdAsync(row.Id, It.IsAny<CancellationToken>())).ReturnsAsync(row);

        var useCase = new RemoveSystemParameterValueUseCase(repository.Object);

        await useCase.ExecuteAsync(SystemParameterName.AutoWorkLogTypes, row.Id);

        repository.Verify(r => r.DeleteAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_IdDoesNotExist_ThrowsNotFoundExceptionAndDoesNotDelete()
    {
        var repository = new Mock<ISystemParameterRepository>();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((SystemParameter?)null);

        var useCase = new RemoveSystemParameterValueUseCase(repository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(SystemParameterName.AutoWorkLogTypes, Guid.NewGuid()));

        repository.Verify(r => r.DeleteAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_IdBelongsToDifferentParam_ThrowsNotFoundExceptionAndDoesNotDelete()
    {
        // The row found by id belongs to a different param than the one requested (e.g. a stale
        // or mismatched id); the use case must not delete it just because the id matched.
        var row = new SystemParameter
        {
            Id = Guid.NewGuid(),
            Param = SystemParameterName.AllowManageClosedWorkLogs,
            Value = "true",
            ValueType = SystemParameterValueType.Bool,
        };

        var repository = new Mock<ISystemParameterRepository>();
        repository.Setup(r => r.GetByIdAsync(row.Id, It.IsAny<CancellationToken>())).ReturnsAsync(row);

        var useCase = new RemoveSystemParameterValueUseCase(repository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(SystemParameterName.AutoWorkLogTypes, row.Id));

        repository.Verify(r => r.DeleteAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ScalarParam_ThrowsDomainExceptionAndDoesNotDelete()
    {
        var repository = new Mock<ISystemParameterRepository>();

        var useCase = new RemoveSystemParameterValueUseCase(repository.Object);

        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(SystemParameterName.AllowManageClosedWorkLogs, Guid.NewGuid()));

        repository.Verify(r => r.DeleteAsync(It.IsAny<SystemParameter>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
