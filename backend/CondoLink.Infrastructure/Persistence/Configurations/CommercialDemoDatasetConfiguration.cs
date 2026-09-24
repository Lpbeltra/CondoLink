using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CommercialDemoDatasetConfiguration : IEntityTypeConfiguration<CommercialDemoDataset>
{
    public void Configure(EntityTypeBuilder<CommercialDemoDataset> builder)
    {
        builder.ToTable("commercial_demo_datasets");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(80);
        builder.Property(x => x.ManifestJson).HasColumnName("manifest_json").IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        // Deliberately no FK to the operator: their account must not prevent safe cleanup.
    }
}
