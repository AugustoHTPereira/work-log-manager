using AutoMapper;
using WorkLogManager.Api.Dtos.Employees;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.UseCases.Employees;

namespace WorkLogManager.Api.Endpoints;

public static class EmployeesEndpoints
{
    public static IEndpointRouteBuilder MapEmployeesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/employees").WithTags("Employees");

        group.MapGet("/", async (ListEmployeesUseCase useCase, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var employees = await useCase.ExecuteAsync(cancellationToken);
            return Results.Ok(mapper.Map<IReadOnlyList<EmployeeSummaryResponse>>(employees));
        });

        group.MapGet("/{id:guid}", async (Guid id, GetEmployeeByIdUseCase useCase, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return Results.Ok(mapper.Map<EmployeeDetailResponse>(result));
        });

        group.MapPost("/", async (CreateEmployeeRequest request, CreateEmployeeUseCase useCase, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var employee = mapper.Map<Employee>(request);
            var created = await useCase.ExecuteAsync(employee, cancellationToken);
            return Results.Created($"/employees/{created.Id}", mapper.Map<EmployeeSummaryResponse>(created));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateEmployeeRequest request, UpdateEmployeeUseCase useCase, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var employee = mapper.Map<Employee>(request);
            var updated = await useCase.ExecuteAsync(id, employee, cancellationToken);
            return Results.Ok(mapper.Map<EmployeeSummaryResponse>(updated));
        });

        group.MapDelete("/{id:guid}", async (Guid id, DeleteEmployeeUseCase useCase, CancellationToken cancellationToken) =>
        {
            await useCase.ExecuteAsync(id, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
