using WorkLogManager.Application.Entities;

namespace WorkLogManager.Application.Interfaces;

public interface IEmployeeRepository
{
    Task AddAsync(Employee employee, CancellationToken cancellationToken = default);
    Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default);
    Task DeleteAsync(Employee employee, CancellationToken cancellationToken = default);
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Employee>> ListAllAsync(CancellationToken cancellationToken = default);
}
