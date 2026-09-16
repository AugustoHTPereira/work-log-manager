using AutoMapper;
using WorkLogManager.Api.Dtos.WorkSchedules;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.UseCases.WorkSchedules;

namespace WorkLogManager.Api.Endpoints;

public static class WorkScheduleEndpoints
{
    public static IEndpointRouteBuilder MapWorkScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/work-schedule").WithTags("WorkSchedule");

        group.MapGet("/", async (GetGeneralWorkScheduleUseCase useCase, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var periods = await useCase.ExecuteAsync(cancellationToken);
            return Results.Ok(mapper.Map<IReadOnlyList<WorkSchedulePeriodResponse>>(periods));
        });

        group.MapPut("/", async (
            UpdateWorkScheduleRequest request,
            UpdateGeneralWorkScheduleUseCase useCase,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            var periods = mapper.Map<IReadOnlyList<WorkSchedulePeriod>>(request.Periods);
            var updated = await useCase.ExecuteAsync(periods, cancellationToken);
            return Results.Ok(mapper.Map<IReadOnlyList<WorkSchedulePeriodResponse>>(updated));
        });

        return app;
    }
}
