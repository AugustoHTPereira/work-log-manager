using AutoMapper;
using WorkLogManager.Api.Dtos.SystemParameters;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Api.Mapping.Profiles;

public class SystemParameterMappingProfile : Profile
{
    public SystemParameterMappingProfile()
    {
        CreateMap<SystemParameter, SystemParameterResponse>();
    }
}
