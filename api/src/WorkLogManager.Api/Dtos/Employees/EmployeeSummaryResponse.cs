namespace WorkLogManager.Api.Dtos.Employees;

public record EmployeeSummaryResponse(Guid Id, string Name, string Role, DateOnly HireDate);
