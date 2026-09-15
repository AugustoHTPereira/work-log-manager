using AutoMapper;
using WorkLogManager.Api.Dtos.SystemSettings;
using WorkLogManager.Application.UseCases.SystemSettings;

namespace WorkLogManager.Api.Endpoints;

public static class SystemSettingsEndpoints
{
    public static IEndpointRouteBuilder MapSystemSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system-settings").WithTags("SystemSettings");

        group.MapGet("/", async (GetSystemSettingsUseCase useCase, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var settings = await useCase.ExecuteAsync(cancellationToken);
            return Results.Ok(mapper.Map<SystemSettingsResponse>(settings));
        });

        group.MapPut("/", async (
            UpdateSystemSettingsRequest request,
            UpdateSystemSettingsUseCase useCase,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            var settings = mapper.Map<Application.Entities.SystemSettings>(request);
            var updated = await useCase.ExecuteAsync(settings, cancellationToken);
            return Results.Ok(mapper.Map<SystemSettingsResponse>(updated));
        });

        return app;
    }
}
