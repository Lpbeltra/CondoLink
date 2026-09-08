using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class RequestServiceProviderHistoryConfiguration : IEntityTypeConfiguration<RequestServiceProviderHistory>
{
    public void Configure(EntityTypeBuilder<RequestServiceProviderHistory> b)
    {
        b.ToTable("request_service_provider_history"); b.HasKey(x=>x.Id);
        b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.RequestId).HasColumnName("request_id"); b.Property(x=>x.EventType).HasColumnName("event_type").HasMaxLength(30).IsRequired(); b.Property(x=>x.PreviousName).HasColumnName("previous_name").HasMaxLength(160); b.Property(x=>x.PreviousSpecialty).HasColumnName("previous_specialty").HasMaxLength(120); b.Property(x=>x.ProviderName).HasColumnName("provider_name").HasMaxLength(160); b.Property(x=>x.ProviderSpecialty).HasColumnName("provider_specialty").HasMaxLength(120); b.Property(x=>x.ChangedByUserId).HasColumnName("changed_by_user_id"); b.Property(x=>x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x=>new{x.RequestId,x.CreatedAt}); b.HasOne<CondoLink.Domain.Entities.Request>().WithMany().HasForeignKey(x=>x.RequestId).OnDelete(DeleteBehavior.Cascade); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
