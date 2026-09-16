using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using WorkLogManager.Api.Endpoints;
using WorkLogManager.Api.Mapping.Profiles;
using WorkLogManager.Api.Middleware;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;
using WorkLogManager.Application.UseCases.EmployeeWorkLogs;
using WorkLogManager.Application.UseCases.Employees;
using WorkLogManager.Application.UseCases.MonthClosings;
using WorkLogManager.Application.UseCases.SystemParameters;
using WorkLogManager.Application.UseCases.WorkSchedules;
using WorkLogManager.Application.Validators;
using WorkLogManager.Infrastructure.Persistence;
using WorkLogManager.Infrastructure.Persistence.Repositories;
using WorkLogManager.Infrastructure.Reporting;
using WorkLogManager.Infrastructure.Services;

// QuestPDF Community license (see the licensing note on WorkLogManager.Infrastructure.csproj):
// free for companies below the revenue threshold or non-commercial use; must be confirmed by
// the project owner before production use.
QuestPDF.Settings.License = LicenseType.Community;

const string FrontendCorsPolicy = "FrontendCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

builder.Services.AddOpenApi();

builder.Services.AddAutoMapper(typeof(EmployeeMappingProfile).Assembly);

builder.Services.AddValidatorsFromAssembly(typeof(EmployeeValidator).Assembly);

builder.Services.AddDbContext<WorkLogManagerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WorkLogManagerDb"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IEmployeeWorkLogRepository, EmployeeWorkLogRepository>();
builder.Services.AddScoped<IWorkSchedulePeriodRepository, WorkSchedulePeriodRepository>();
builder.Services.AddScoped<IMonthClosingRepository, MonthClosingRepository>();
builder.Services.AddScoped<IMonthClosingReportRenderer, MonthClosingReportRenderer>();
builder.Services.AddScoped<ISystemParameterRepository, SystemParameterRepository>();
builder.Services.AddScoped<IRandomProvider, SystemRandomProvider>();

builder.Services.AddScoped<CreateEmployeeUseCase>();
builder.Services.AddScoped<UpdateEmployeeUseCase>();
builder.Services.AddScoped<DeleteEmployeeUseCase>();
builder.Services.AddScoped<GetEmployeeByIdUseCase>();
builder.Services.AddScoped<ListEmployeesUseCase>();

builder.Services.AddScoped<CreateEmployeeWorkLogUseCase>();
builder.Services.AddScoped<UpdateEmployeeWorkLogUseCase>();
builder.Services.AddScoped<DeleteEmployeeWorkLogUseCase>();
builder.Services.AddScoped<ListEmployeeWorkLogsUseCase>();

builder.Services.AddScoped<GetGeneralWorkScheduleUseCase>();
builder.Services.AddScoped<UpdateGeneralWorkScheduleUseCase>();
builder.Services.AddScoped<GetEmployeeWorkScheduleUseCase>();
builder.Services.AddScoped<UpdateEmployeeWorkScheduleUseCase>();

builder.Services.AddScoped<WorkLogGenerationService>();
builder.Services.AddScoped<CloseMonthUseCase>();
builder.Services.AddScoped<GenerateMonthClosingReportUseCase>();
builder.Services.AddScoped<ListMonthClosingsUseCase>();
builder.Services.AddScoped<DeleteMonthClosingUseCase>();

builder.Services.AddScoped<ListSystemParametersUseCase>();
builder.Services.AddScoped<ListSystemParameterValuesByNameUseCase>();
builder.Services.AddScoped<UpdateSystemParameterUseCase>();
builder.Services.AddScoped<AddSystemParameterValueUseCase>();
builder.Services.AddScoped<RemoveSystemParameterValueUseCase>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WorkLogManagerDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapEmployeesEndpoints();
app.MapEmployeeWorkLogsEndpoints();
app.MapWorkScheduleEndpoints();
app.MapMonthClosingsEndpoints();
app.MapSystemParametersEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program
{
}
