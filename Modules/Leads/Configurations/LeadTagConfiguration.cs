using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSForge.Api.Modules.Leads.Entities;

namespace SaaSForge.Api.Modules.Leads.Configurations;

public class LeadTagConfiguration : IEntityTypeConfiguration<LeadTag>
{
    public void Configure(EntityTypeBuilder<LeadTag> builder)
    {
        builder.ToTable("LeadTags");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(30);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.BusinessId, x.Name }).IsUnique();
    }
}