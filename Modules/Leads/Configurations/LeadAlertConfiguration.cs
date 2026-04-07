using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSForge.Api.Modules.Leads.Entities;

namespace SaaSForge.Api.Modules.Leads.Configurations;

public sealed class LeadAlertConfiguration : IEntityTypeConfiguration<LeadAlert>
{
    public void Configure(EntityTypeBuilder<LeadAlert> builder)
    {
        builder.ToTable("LeadAlerts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.Severity)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.IsResolved)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Lead)
            .WithMany(x => x.Alerts)
            .HasForeignKey(x => x.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.BusinessId, x.IsResolved, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.LeadId, x.Type, x.IsResolved });
    }
}