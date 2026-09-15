using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees", t => t.HasCheckConstraint(
            "ck_employees_daily_work_hours_positive",
            "daily_work_hours IS NULL OR daily_work_hours > 0"));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Role)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.HireDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.DailyWorkHours)
            .HasColumnType("numeric(5,2)");

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .IsRequired();
    }
}
