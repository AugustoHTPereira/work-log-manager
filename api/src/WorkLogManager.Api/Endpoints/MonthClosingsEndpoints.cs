using AutoMapper;
using WorkLogManager.Api.Dtos.MonthClosings;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.UseCases.MonthClosings;

namespace WorkLogManager.Api.Endpoints;

public static class MonthClosingsEndpoints
{
    public static IEndpointRouteBuilder MapMonthClosingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/month-closings").WithTags("MonthClosings");

        group.MapPost("/", async (
            CreateMonthClosingRequest request,
            CloseMonthUseCase useCase,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            var monthClosing = mapper.Map<MonthClosing>(request);
            var result = await useCase.ExecuteAsync(monthClosing, cancellationToken);
            return Results.Created($"/month-closings/{result.MonthClosing.Id}", mapper.Map<MonthClosingResponse>(result));
        });

        return app;
    }
}
