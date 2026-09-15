using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Infrastructure.Persistence.Configurations;

public class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("system_settings", t => t.HasCheckConstraint(
            "ck_system_settings_default_daily_work_hours_positive",
            "default_daily_work_hours > 0"));

        builder.HasKey(s => s.Id);

        builder.Property(s => s.DefaultDailyWorkHours)
            .HasColumnType("numeric(5,2)")
            .HasDefaultValue(8.00m)
            .IsRequired();

        builder.Property(s => s.UpdatedAtUtc)
            .IsRequired();

        builder.HasData(new
        {
            Id = SystemSettings.SingletonId,
            DefaultDailyWorkHours = 8.00m,
            UpdatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
    }
}
