using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class OperationalMessageTemplateConfiguration
    : IEntityTypeConfiguration<OperationalMessageTemplate>
{
    public void Configure(EntityTypeBuilder<OperationalMessageTemplate> b)
    {
        b.ToTable("operational_message_templates");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasColumnName("key").HasMaxLength(80);
        b.Property(x => x.Prefix).HasColumnName("prefix").HasMaxLength(1200).IsRequired();
        b.Property(x => x.Suffix).HasColumnName("suffix").HasMaxLength(1200).IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
        b.HasIndex(x => x.UpdatedAt);
    }
}
