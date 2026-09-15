using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Infrastructure.Persistence.Configurations;

public class MonthClosingConfiguration : IEntityTypeConfiguration<MonthClosing>
{
    public void Configure(EntityTypeBuilder<MonthClosing> builder)
    {
        builder.ToTable("month_closings", t => t.HasCheckConstraint(
            "ck_month_closings_month_range",
            "month >= 1 AND month <= 12"));

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Month)
            .IsRequired();

        builder.Property(m => m.Year)
            .IsRequired();

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        builder.Property(m => m.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(m => new { m.Year, m.Month })
            .IsUnique();
    }
}
