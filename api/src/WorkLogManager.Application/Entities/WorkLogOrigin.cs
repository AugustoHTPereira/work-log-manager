namespace WorkLogManager.Application.Entities;

/// <summary>
/// How an <see cref="EmployeeWorkLog"/> came to exist: generated automatically by the
/// "close month" flow (<see cref="UseCases.MonthClosings.CloseMonthUseCase"/> via
/// <see cref="Services.WorkLogGenerationService"/>) or created manually by a user through
/// <see cref="UseCases.EmployeeWorkLogs.CreateEmployeeWorkLogUseCase"/>.
/// </summary>
public enum WorkLogOrigin
{
    Automatic,
    Manual
}
