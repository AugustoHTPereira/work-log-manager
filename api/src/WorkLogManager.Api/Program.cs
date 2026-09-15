using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkLogManager.Api.Endpoints;
using WorkLogManager.Api.Mapping.Profiles;
using WorkLogManager.Api.Middleware;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.Services;
using WorkLogManager.Application.UseCases.EmployeeWorkLogs;
using WorkLogManager.Application.UseCases.Employees;
using WorkLogManager.Application.UseCases.MonthClosings;
using WorkLogManager.Application.UseCases.SystemSettings;
using WorkLogManager.Application.Validators;
using WorkLogManager.Infrastructure.Persistence;
using WorkLogManager.Infrastructure.Persistence.Repositories;
using WorkLogManager.Infrastructure.Services;

const string FrontendCorsPolicy = "FrontendCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddAutoMapper(typeof(EmployeeMappingProfile).Assembly);

builder.Services.AddValidatorsFromAssembly(typeof(EmployeeValidator).Assembly);

builder.Services.AddDbContext<WorkLogManagerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WorkLogManagerDb"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IEmployeeWorkLogRepository, EmployeeWorkLogRepository>();
builder.Services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
builder.Services.AddScoped<IMonthClosingRepository, MonthClosingRepository>();
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

builder.Services.AddScoped<GetSystemSettingsUseCase>();
builder.Services.AddScoped<UpdateSystemSettingsUseCase>();

builder.Services.AddScoped<WorkLogGenerationService>();
builder.Services.AddScoped<CloseMonthUseCase>();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.MapEmployeesEndpoints();
app.MapEmployeeWorkLogsEndpoints();
app.MapSystemSettingsEndpoints();
app.MapMonthClosingsEndpoints();

app.Run();

public partial class Program
{
}
