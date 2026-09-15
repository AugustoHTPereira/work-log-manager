using AutoMapper;
using WorkLogManager.Api.Dtos.EmployeeWorkLogs;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.UseCases.EmployeeWorkLogs;

namespace WorkLogManager.Api.Endpoints;

public static class EmployeeWorkLogsEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeWorkLogsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/employees/{employeeId:guid}/work-logs").WithTags("EmployeeWorkLogs");

        group.MapPost("/", async (
            Guid employeeId,
            CreateEmployeeWorkLogRequest request,
            CreateEmployeeWorkLogUseCase useCase,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            var workLog = mapper.Map<EmployeeWorkLog>(request);
            var created = await useCase.ExecuteAsync(employeeId, workLog, cancellationToken);
            return Results.Created($"/employees/{employeeId}/work-logs/{created.Id}", mapper.Map<EmployeeWorkLogResponse>(created));
        });

        group.MapPut("/{id:guid}", async (
            Guid employeeId,
            Guid id,
            UpdateEmployeeWorkLogRequest request,
            UpdateEmployeeWorkLogUseCase useCase,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            var workLog = mapper.Map<EmployeeWorkLog>(request);
            var updated = await useCase.ExecuteAsync(employeeId, id, workLog, cancellationToken);
            return Results.Ok(mapper.Map<EmployeeWorkLogResponse>(updated));
        });

        group.MapDelete("/{id:guid}", async (
            Guid employeeId,
            Guid id,
            DeleteEmployeeWorkLogUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            await useCase.ExecuteAsync(employeeId, id, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
