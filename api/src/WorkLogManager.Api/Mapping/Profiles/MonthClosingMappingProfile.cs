using AutoMapper;
using WorkLogManager.Api.Dtos.MonthClosings;
using WorkLogManager.Application.Entities;
using WorkLogManager.Application.Results;

namespace WorkLogManager.Api.Mapping.Profiles;

public class MonthClosingMappingProfile : Profile
{
    public MonthClosingMappingProfile()
    {
        // Request -> Entity: Id/CreatedAtUtc/UpdatedAtUtc are assigned by the use-case,
        // never by the client.
        CreateMap<CreateMonthClosingRequest, MonthClosing>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAtUtc, opt => opt.Ignore());

        CreateMap<MonthClosingResult, MonthClosingResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.MonthClosing.Id))
            .ForMember(dest => dest.Month, opt => opt.MapFrom(src => src.MonthClosing.Month))
            .ForMember(dest => dest.Year, opt => opt.MapFrom(src => src.MonthClosing.Year))
            .ForMember(dest => dest.CreatedAtUtc, opt => opt.MapFrom(src => src.MonthClosing.CreatedAtUtc))
            .ForMember(dest => dest.Summaries, opt => opt.MapFrom(src => src.Summaries));

        CreateMap<EmployeeWorkLogGenerationSummary, EmployeeWorkLogGenerationSummaryResponse>();

        // Listing only: the per-employee generation summaries exist just in the close-month result.
        CreateMap<MonthClosing, MonthClosingResponse>()
            .ForMember(dest => dest.Summaries, opt => opt.Ignore());

        CreateMap<MonthClosingDeletionResult, DeleteMonthClosingResponse>();
    }
}
