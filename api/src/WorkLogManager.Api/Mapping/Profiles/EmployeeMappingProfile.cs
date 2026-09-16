using AutoMapper;
using WorkLogManager.Api.Dtos.Employees;
using WorkLogManager.Application.Entities;

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

        CreateMap<Employee, EmployeeDetailResponse>();
    }
}
