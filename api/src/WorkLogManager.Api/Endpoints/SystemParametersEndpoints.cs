using AutoMapper;
using WorkLogManager.Api.Dtos.SystemParameters;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.UseCases.SystemParameters;

namespace WorkLogManager.Api.Endpoints;

public static class SystemParametersEndpoints
{
    public static IEndpointRouteBuilder MapSystemParametersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/settings").WithTags("SystemParameters");

        group.MapGet("/", async (
            string[]? param,
            ListSystemParametersUseCase useCase,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyList<SystemParameterName>? paramFilter = null;

            if (param is { Length: > 0 })
            {
                var parsed = new List<SystemParameterName>();
                var invalid = new List<string>();

                foreach (var value in param)
                {
                    if (Enum.TryParse<SystemParameterName>(value, ignoreCase: true, out var parsedParam))
                    {
                        parsed.Add(parsedParam);
                    }
                    else
                    {
                        invalid.Add(value);
                    }
                }

                if (invalid.Count > 0)
                {
                    return Results.BadRequest(new { message = $"Invalid system parameter name(s): {string.Join(", ", invalid)}." });
                }

                paramFilter = parsed;
            }

            var parameters = await useCase.ExecuteAsync(paramFilter, cancellationToken);
            return Results.Ok(mapper.Map<IReadOnlyList<SystemParameterResponse>>(parameters));
        });

        group.MapGet("/{param}", async (
            string param,
            ListSystemParameterValuesByNameUseCase useCase,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<SystemParameterName>(param, ignoreCase: true, out var parsedParam))
            {
                return Results.BadRequest(new { message = $"Invalid system parameter name: {param}." });
            }

            var parameters = await useCase.ExecuteAsync(parsedParam, cancellationToken);
            return Results.Ok(mapper.Map<IReadOnlyList<SystemParameterResponse>>(parameters));
        });

        group.MapPut("/{param}", async (
            string param,
            UpdateSystemParameterRequest request,
            UpdateSystemParameterUseCase useCase,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<SystemParameterName>(param, ignoreCase: true, out var parsedParam))
            {
                return Results.BadRequest(new { message = $"Invalid system parameter name: {param}." });
            }

            var updated = await useCase.ExecuteAsync(parsedParam, request.Value, cancellationToken);
            return Results.Ok(mapper.Map<SystemParameterResponse>(updated));
        });

        group.MapPost("/{param}", async (
            string param,
            AddSystemParameterValueRequest request,
            AddSystemParameterValueUseCase useCase,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<SystemParameterName>(param, ignoreCase: true, out var parsedParam))
            {
                return Results.BadRequest(new { message = $"Invalid system parameter name: {param}." });
            }

            var added = await useCase.ExecuteAsync(parsedParam, request.Value, cancellationToken);
            return Results.Ok(mapper.Map<SystemParameterResponse>(added));
        });

        group.MapDelete("/{param}/{id:guid}", async (
            string param,
            Guid id,
            RemoveSystemParameterValueUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<SystemParameterName>(param, ignoreCase: true, out var parsedParam))
            {
                return Results.BadRequest(new { message = $"Invalid system parameter name: {param}." });
            }

            await useCase.ExecuteAsync(parsedParam, id, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
