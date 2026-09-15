using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkLogManager.Api.Endpoints;
using WorkLogManager.Api.Mapping.Profiles;
using WorkLogManager.Api.Middleware;
using WorkLogManager.Application.Interfaces;
using WorkLogManager.Application.UseCases.EmployeeWorkLogs;
using WorkLogManager.Application.UseCases.Employees;
using WorkLogManager.Application.UseCases.SystemSettings;
using WorkLogManager.Application.Validators;
using WorkLogManager.Infrastructure.Persistence;
using WorkLogManager.Infrastructure.Persistence.Repositories;

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

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

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

app.Run();

public partial class Program
{
}
