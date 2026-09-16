using AutoMapper;
using WorkLogManager.Api.Dtos.WorkSchedules;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Api.Mapping.Profiles;

public class WorkScheduleMappingProfile : Profile
{
    public WorkScheduleMappingProfile()
    {
        // Request -> Entity: Id/EmployeeId/CreatedAtUtc/UpdatedAtUtc are all assigned by the
        // use-case (the request semantics are always "replace everything").
        CreateMap<WorkSchedulePeriodInputDto, WorkSchedulePeriod>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.EmployeeId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAtUtc, opt => opt.Ignore());

        CreateMap<WorkSchedulePeriod, WorkSchedulePeriodResponse>();
    }
}
