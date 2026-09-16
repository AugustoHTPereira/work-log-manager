using Moq;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.UseCases.SystemParameters;

namespace WorkLogManager.Application.Tests.UseCases.SystemParameters;

public class ListSystemParametersUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_NoFilter_ReturnsEverythingFromRepository()
    {
        var repository = new Mock<ISystemParameterRepository>();
        var parameters = new List<SystemParameter>
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
                Param = SystemParameterName.AllowManageClosedWorkLogs,
                Value = "false",
                ValueType = SystemParameterValueType.Bool,
            },
        };
        repository
            .Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parameters);

        var useCase = new ListSystemParametersUseCase(repository.Object);

        var result = await useCase.ExecuteAsync();

        Assert.Equal(parameters, result);
    }

    [Fact]
    public async Task ExecuteAsync_WithFilter_ForwardsFilterToRepository()
    {
        var repository = new Mock<ISystemParameterRepository>();
        var filter = new List<SystemParameterName> { SystemParameterName.AllowManageClosedWorkLogs };
        repository
            .Setup(r => r.ListAsync(filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var useCase = new ListSystemParametersUseCase(repository.Object);

        var result = await useCase.ExecuteAsync(filter);

        Assert.Empty(result);
        repository.Verify(r => r.ListAsync(filter, It.IsAny<CancellationToken>()), Times.Once);
    }
}
