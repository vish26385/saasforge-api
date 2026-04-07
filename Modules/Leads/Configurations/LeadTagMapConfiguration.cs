using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSForge.Api.Modules.Leads.Entities;

namespace SaaSForge.Api.Modules.Leads.Configurations;

public class LeadTagMapConfiguration : IEntityTypeConfiguration<LeadTagMap>
{
    public void Configure(EntityTypeBuilder<LeadTagMap> builder)
    {
        builder.ToTable("LeadTagMaps");

        builder.HasKey(x => new { x.LeadId, x.TagId });

        builder.HasOne(x => x.Lead)
            .WithMany(x => x.LeadTags)
            .HasForeignKey(x => x.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Tag)
            .WithMany(x => x.LeadTagMaps)
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}