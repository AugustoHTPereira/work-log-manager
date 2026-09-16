using AutoMapper;
using Microsoft.AspNetCore.Mvc;
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

        group.MapGet("/", async (
            ListMonthClosingsUseCase useCase,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(cancellationToken);
            return Results.Ok(mapper.Map<IEnumerable<MonthClosingResponse>>(result));
        });

        group.MapGet("/{id:guid}/report", async (
            Guid id,
            GenerateMonthClosingReportUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var file = await useCase.ExecuteAsync(id, cancellationToken);
            return Results.File(file.Content, "application/pdf", file.FileName);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            DeleteMonthClosingUseCase useCase,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return Results.Ok(mapper.Map<DeleteMonthClosingResponse>(result));
        });

        return app;
    }
}
