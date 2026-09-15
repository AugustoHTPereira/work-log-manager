using AutoMapper;
using WorkLogManager.Api.Dtos.SystemSettings;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Api.Mapping.Profiles;

public class SystemSettingsMappingProfile : Profile
{
    public SystemSettingsMappingProfile()
    {
        CreateMap<SystemSettings, SystemSettingsResponse>();

        // Request -> Entity: Id/UpdatedAtUtc are assigned by the use-case (Id is the
        // well-known singleton id, never supplied by the client).
        CreateMap<UpdateSystemSettingsRequest, SystemSettings>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAtUtc, opt => opt.Ignore());
    }
}
