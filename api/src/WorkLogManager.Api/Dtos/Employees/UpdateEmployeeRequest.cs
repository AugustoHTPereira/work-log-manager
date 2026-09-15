namespace WorkLogManager.Api.Dtos.Employees;

public record UpdateEmployeeRequest(string Name, string Role, DateOnly HireDate, decimal? DailyWorkHours);
