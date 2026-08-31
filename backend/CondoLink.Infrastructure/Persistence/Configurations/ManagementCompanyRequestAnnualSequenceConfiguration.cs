using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class ManagementCompanyRequestAnnualSequenceConfiguration : IEntityTypeConfiguration<ManagementCompanyRequestAnnualSequence>
{
    public void Configure(EntityTypeBuilder<ManagementCompanyRequestAnnualSequence> b)
    {
        b.ToTable("management_company_request_annual_sequences");
        b.HasKey(x => x.Year);
        b.Property(x => x.Year).HasColumnName("year").ValueGeneratedNever();
        b.Property(x => x.LastValue).HasColumnName("last_value");
    }
}
