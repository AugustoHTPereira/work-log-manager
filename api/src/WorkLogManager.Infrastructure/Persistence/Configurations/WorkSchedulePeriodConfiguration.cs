using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Infrastructure.Persistence.Configurations;

public class WorkSchedulePeriodConfiguration : IEntityTypeConfiguration<WorkSchedulePeriod>
{
    public void Configure(EntityTypeBuilder<WorkSchedulePeriod> builder)
    {
        builder.ToTable("work_schedule_periods", t => t.HasCheckConstraint(
            "ck_work_schedule_periods_end_after_start",
            "end_time > start_time"));

        builder.HasKey(p => p.Id);

        builder.Property(p => p.EmployeeId);

        builder.Property(p => p.DayOfWeek)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.StartTime)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(p => p.EndTime)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(p => new { p.EmployeeId, p.DayOfWeek })
            .HasDatabaseName("ix_work_schedule_periods_employee_id_day_of_week");

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
