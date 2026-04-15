using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaSForge.Api.Modules.Leads.Entities;

namespace SaaSForge.Api.Modules.Leads.Configurations;

public class LeadAiSuggestionConfiguration : IEntityTypeConfiguration<LeadAiSuggestion>
{
    //public void Configure(EntityTypeBuilder<LeadAiSuggestion> builder)
    //{
    //    builder.ToTable("LeadAiSuggestions");

    //    builder.HasKey(x => x.Id);

    //    builder.Property(x => x.SuggestionType).HasMaxLength(50).IsRequired();
    //    builder.Property(x => x.InputContext).HasMaxLength(12000).IsRequired();
    //    builder.Property(x => x.OutputText).HasMaxLength(12000).IsRequired();
    //    builder.Property(x => x.Tone).HasMaxLength(50);
    //    builder.Property(x => x.Goal).HasMaxLength(50);
    //    builder.Property(x => x.Model).HasMaxLength(100);
    //    builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");

    //    builder.HasOne(x => x.Lead)
    //        .WithMany(x => x.AiSuggestions)
    //        .HasForeignKey(x => x.LeadId)
    //        .OnDelete(DeleteBehavior.Cascade);

    //    builder.HasIndex(x => new { x.BusinessId, x.LeadId, x.CreatedAtUtc });
    //}

    public void Configure(EntityTypeBuilder<LeadAiSuggestion> builder)
    {
        builder.ToTable("LeadAiSuggestions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SuggestionType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.InputContext)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.OutputText)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Tone)
            .HasMaxLength(50);

        builder.Property(x => x.Goal)
            .HasMaxLength(50);

        builder.Property(x => x.Model)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasOne(x => x.Lead)
            .WithMany(x => x.AiSuggestions)
            .HasForeignKey(x => x.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.BusinessId, x.LeadId, x.CreatedAtUtc });
    }
}