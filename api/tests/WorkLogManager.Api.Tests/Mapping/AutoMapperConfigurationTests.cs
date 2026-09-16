using AutoMapper;
using WorkLogManager.Api.Mapping.Profiles;

namespace WorkLogManager.Api.Tests.Mapping;

public class AutoMapperConfigurationTests
{
    [Fact]
    public void Configuration_AllProfiles_AreValid()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<EmployeeMappingProfile>();
            cfg.AddProfile<EmployeeWorkLogMappingProfile>();
            cfg.AddProfile<WorkScheduleMappingProfile>();
            cfg.AddProfile<MonthClosingMappingProfile>();
        });

        configuration.AssertConfigurationIsValid();
    }
}
