using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class RequestServiceProviderLinkConfiguration : IEntityTypeConfiguration<CondoLink.Domain.Entities.Request>
{
    public void Configure(EntityTypeBuilder<CondoLink.Domain.Entities.Request> b)
    { b.Property(x=>x.ServiceProviderId).HasColumnName("service_provider_id"); b.HasIndex(x=>x.ServiceProviderId); b.HasOne<ServiceProvider>().WithMany().HasForeignKey(x=>x.ServiceProviderId).OnDelete(DeleteBehavior.SetNull); }
}
