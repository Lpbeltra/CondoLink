using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CondominiumModuleConfiguration : IEntityTypeConfiguration<CondominiumModule>
{
    public void Configure(EntityTypeBuilder<CondominiumModule> b)
    {
        b.ToTable("condominium_modules"); b.HasKey(x => x.Id);
        b.Property(x => x.Module).HasConversion<int>();
        b.HasIndex(x => new { x.CondominiumId, x.Module }).IsUnique();
        b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Cascade);
    }
}
