using Microsoft.EntityFrameworkCore;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Infrastructure.Persistence;

public class WorkLogManagerDbContext : DbContext
{
    public WorkLogManagerDbContext(DbContextOptions<WorkLogManagerDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeWorkLog> EmployeeWorkLogs => Set<EmployeeWorkLog>();
    public DbSet<WorkSchedulePeriod> WorkSchedulePeriods => Set<WorkSchedulePeriod>();
    public DbSet<MonthClosing> MonthClosings => Set<MonthClosing>();
    public DbSet<SystemParameter> SystemParameters => Set<SystemParameter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkLogManagerDbContext).Assembly);
    }
}
