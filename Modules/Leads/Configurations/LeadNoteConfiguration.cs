using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSForge.Api.Modules.Leads.Entities;

namespace SaaSForge.Api.Modules.Leads.Configurations;

public class LeadNoteConfiguration : IEntityTypeConfiguration<LeadNote>
{
    public void Configure(EntityTypeBuilder<LeadNote> builder)
    {
        builder.ToTable("LeadNotes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Note).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasOne(x => x.Lead)
            .WithMany(x => x.Notes)
            .HasForeignKey(x => x.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.BusinessId, x.LeadId, x.CreatedAtUtc });
    }
}