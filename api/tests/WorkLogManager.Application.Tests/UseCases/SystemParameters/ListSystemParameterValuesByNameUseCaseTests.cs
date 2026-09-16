using Moq;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.UseCases.SystemParameters;

namespace WorkLogManager.Application.Tests.UseCases.SystemParameters;

public class ListSystemParameterValuesByNameUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ScalarParamConfigured_ReturnsSingleRow()
    {
        var repository = new Mock<ISystemParameterRepository>();
        var parameter = new SystemParameter
        {
            Id = Guid.NewGuid(),
            Param = SystemParameterName.AllowManageClosedWorkLogs,
            Value = "true",
            ValueType = SystemParameterValueType.Bool,
        };
        repository
            .Setup(r => r.ListByParamAsync(SystemParameterName.AllowManageClosedWorkLogs, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)[parameter]);

        var useCase = new ListSystemParameterValuesByNameUseCase(repository.Object);

        var result = await useCase.ExecuteAsync(SystemParameterName.AllowManageClosedWorkLogs);

        Assert.Single(result);
        Assert.Same(parameter, result[0]);
    }

    [Fact]
    public async Task ExecuteAsync_ArrayParamWithMultipleRows_ReturnsAllRows()
    {
        var repository = new Mock<ISystemParameterRepository>();
        var rows = new List<SystemParameter>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Param = SystemParameterName.AutoWorkLogTypes,
                Value = "RegularAttendance",
                ValueType = SystemParameterValueType.Array,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Param = SystemParameterName.AutoWorkLogTypes,
                Value = "Break",
                ValueType = SystemParameterValueType.Array,
            },
        };
        repository
            .Setup(r => r.ListByParamAsync(SystemParameterName.AutoWorkLogTypes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var useCase = new ListSystemParameterValuesByNameUseCase(repository.Object);

        var result = await useCase.ExecuteAsync(SystemParameterName.AutoWorkLogTypes);

        Assert.Equal(rows, result);
    }

    [Fact]
    public async Task ExecuteAsync_NeverConfiguredParameter_ReturnsEmptyList()
    {
        var repository = new Mock<ISystemParameterRepository>();
        repository
            .Setup(r => r.ListByParamAsync(It.IsAny<SystemParameterName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemParameter>)[]);

        var useCase = new ListSystemParameterValuesByNameUseCase(repository.Object);

        var result = await useCase.ExecuteAsync(SystemParameterName.AutoWorkLogTypes);

        Assert.Empty(result);
    }
}
