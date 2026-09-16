using AutoMapper;
using WorkLogManager.Api.Dtos.EmployeeWorkLogs;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Api.Mapping.Profiles;

public class EmployeeWorkLogMappingProfile : Profile
{
    public EmployeeWorkLogMappingProfile()
    {
        // Request -> Entity: EmployeeId/Id/DurationSeconds/CreatedAtUtc/UpdatedAtUtc are all
        // assigned or derived by the use-case (DurationSeconds is computed from Start/EndDate,
        // never received as raw input from the client).
        CreateMap<CreateEmployeeWorkLogRequest, EmployeeWorkLog>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.EmployeeId, opt => opt.Ignore())
            .ForMember(dest => dest.DurationSeconds, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAtUtc, opt => opt.Ignore())
            .ForMember(dest => dest.MonthClosingId, opt => opt.Ignore())
            .ForMember(dest => dest.Origin, opt => opt.Ignore());

        CreateMap<UpdateEmployeeWorkLogRequest, EmployeeWorkLog>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.EmployeeId, opt => opt.Ignore())
            .ForMember(dest => dest.DurationSeconds, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAtUtc, opt => opt.Ignore())
            .ForMember(dest => dest.MonthClosingId, opt => opt.Ignore())
            .ForMember(dest => dest.Origin, opt => opt.Ignore());

        CreateMap<EmployeeWorkLog, EmployeeWorkLogResponse>();
    }
}
