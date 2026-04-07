using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSForge.Api.Modules.Leads.Entities;

namespace SaaSForge.Api.Modules.Leads.Configurations;

public class LeadMessageConfiguration : IEntityTypeConfiguration<LeadMessage>
{
    public void Configure(EntityTypeBuilder<LeadMessage> builder)
    {
        builder.ToTable("LeadMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Direction).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(50).IsRequired();
        builder.Property(x => x.MessageType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Content).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.AiTone).HasMaxLength(50);
        builder.Property(x => x.AiGoal).HasMaxLength(50);
        builder.Property(x => x.AiModel).HasMaxLength(100);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasOne(x => x.Lead)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.BusinessId, x.LeadId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.BusinessId, x.CreatedAtUtc });
    }
}