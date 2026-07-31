using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class RequestAiAnalysisConfiguration
    : IEntityTypeConfiguration<RequestAiAnalysis>
{
    public void Configure(EntityTypeBuilder<RequestAiAnalysis> builder)
    {
        builder.ToTable("request_ai_analyses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(x => x.GeneratedTitle).HasColumnName("generated_title")
            .HasMaxLength(200).IsRequired();
        builder.Property(x => x.GeneratedDescription).HasColumnName("generated_description")
            .HasMaxLength(4000).IsRequired();
        builder.Property(x => x.SuggestedCategoryName).HasColumnName("suggested_category_name")
            .HasMaxLength(200);
        builder.Property(x => x.Confidence).HasColumnName("confidence");
        builder.Property(x => x.MissingInformationJson)
            .HasColumnName("missing_information_json").IsRequired();
        builder.Property(x => x.AiModel).HasColumnName("ai_model").HasMaxLength(100);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<DomainRequest>().WithOne()
            .HasForeignKey<RequestAiAnalysis>(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.RequestId).IsUnique();
    }
}
