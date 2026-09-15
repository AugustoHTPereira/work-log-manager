using AutoMapper;
using WorkLogManager.Api.Dtos.Employees;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Results;

namespace WorkLogManager.Api.Mapping.Profiles;

public class EmployeeMappingProfile : Profile
{
    public EmployeeMappingProfile()
    {
        // Request -> Entity: only the fields the user actually supplies are mapped.
        // Id/CreatedAtUtc/UpdatedAtUtc are assigned by the use-case, never by the client.
        CreateMap<CreateEmployeeRequest, Employee>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAtUtc, opt => opt.Ignore());

        CreateMap<UpdateEmployeeRequest, Employee>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAtUtc, opt => opt.Ignore());

        CreateMap<Employee, EmployeeSummaryResponse>();

        CreateMap<EmployeeDetailResult, EmployeeDetailResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Employee.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Employee.Name))
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Employee.Role))
            .ForMember(dest => dest.HireDate, opt => opt.MapFrom(src => src.Employee.HireDate))
            .ForMember(dest => dest.DailyWorkHours, opt => opt.MapFrom(src => src.Employee.DailyWorkHours))
            .ForMember(dest => dest.EffectiveDailyWorkHours, opt => opt.MapFrom(src => src.EffectiveDailyWorkHours))
            .ForMember(dest => dest.WorkLogs, opt => opt.MapFrom(src => src.WorkLogs));
    }
}
