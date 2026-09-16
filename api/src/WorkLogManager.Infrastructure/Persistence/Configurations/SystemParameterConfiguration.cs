using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkLogManager.Application.Entities;

namespace WorkLogManager.Infrastructure.Persistence.Configurations;

public class SystemParameterConfiguration : IEntityTypeConfiguration<SystemParameter>
{
    public void Configure(EntityTypeBuilder<SystemParameter> builder)
    {
        builder.ToTable("system_parameters");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Param)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Value)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.ValueType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(p => new { p.Param, p.Value })
            .HasDatabaseName("ix_system_parameters_param_value")
            .IsUnique();
    }
}
