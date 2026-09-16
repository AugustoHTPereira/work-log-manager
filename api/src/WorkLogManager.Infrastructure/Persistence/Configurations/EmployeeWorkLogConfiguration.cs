using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Infrastructure.Persistence.Configurations;

public class EmployeeWorkLogConfiguration : IEntityTypeConfiguration<EmployeeWorkLog>
{
    public void Configure(EntityTypeBuilder<EmployeeWorkLog> builder)
    {
        builder.ToTable("employee_work_logs", t => t.HasCheckConstraint(
            "ck_employee_work_logs_end_date_after_start_date",
            "end_date >= start_date"));

        builder.HasKey(w => w.Id);

        builder.Property(w => w.EmployeeId)
            .IsRequired();

        builder.Property(w => w.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(w => w.StartDate)
            .IsRequired();

        builder.Property(w => w.EndDate)
            .IsRequired();

        builder.Property(w => w.DurationSeconds)
            .IsRequired();

        builder.Property(w => w.CreatedAtUtc)
            .IsRequired();

        builder.Property(w => w.UpdatedAtUtc)
            .IsRequired();

        builder.Property(w => w.MonthClosingId);

        builder.Property(w => w.Note)
            .HasMaxLength(255);

        builder.Property(w => w.Origin)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(w => w.EmployeeId);

        builder.HasIndex(w => w.MonthClosingId);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(w => w.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<MonthClosing>()
            .WithMany()
            .HasForeignKey(w => w.MonthClosingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
