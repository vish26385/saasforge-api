using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSForge.Api.Modules.Leads.Entities;

namespace SaaSForge.Api.Modules.Leads.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.Source).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(20).IsRequired();
        builder.Property(x => x.InquirySummary).HasMaxLength(2000);
        builder.Property(x => x.LastIncomingMessagePreview).HasMaxLength(1000);
        builder.Property(x => x.EstimatedValue).HasPrecision(18, 2);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastContactAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastReplyAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastIncomingAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.NextFollowUpAtUtc).HasColumnType("timestamp with time zone");

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.BusinessId, x.Status });
        builder.HasIndex(x => new { x.BusinessId, x.Source });
        builder.HasIndex(x => new { x.BusinessId, x.NextFollowUpAtUtc });
        builder.HasIndex(x => new { x.BusinessId, x.Status, x.NextFollowUpAtUtc });
        builder.HasIndex(x => new { x.BusinessId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.BusinessId, x.IsArchived });
        builder.HasIndex(x => new { x.BusinessId, x.Email });
        builder.HasIndex(x => new { x.BusinessId, x.Phone });
    }
}