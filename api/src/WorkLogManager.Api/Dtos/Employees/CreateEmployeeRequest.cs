namespace WorkLogManager.Api.Dtos.Employees;

public record CreateEmployeeRequest(string Name, string Role, DateOnly HireDate, decimal? DailyWorkHours);
