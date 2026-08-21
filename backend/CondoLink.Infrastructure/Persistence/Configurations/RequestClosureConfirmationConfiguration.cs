using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class RequestClosureConfirmationConfiguration : IEntityTypeConfiguration<RequestClosureConfirmation>
{
    public void Configure(EntityTypeBuilder<RequestClosureConfirmation> b)
    {
        b.ToTable("request_closure_confirmations"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.RequestId).HasColumnName("request_id");
        b.Property(x => x.RequestStatusHistoryId).HasColumnName("request_status_history_id");
        b.Property(x => x.Conclusion).HasColumnName("conclusion").HasMaxLength(1000).IsRequired();
        b.Property(x => x.RequestedAt).HasColumnName("requested_at"); b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.Status).HasColumnName("status"); b.Property(x => x.DecidedAt).HasColumnName("decided_at");
        b.Property(x => x.ResponseMessageId).HasColumnName("response_message_id");
        b.Property(x => x.FinalizedAutomatically).HasColumnName("finalized_automatically");
        b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => x.RequestId).HasFilter("status = 1").IsUnique(); b.HasIndex(x => new { x.Status, x.ExpiresAt });
        b.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<RequestStatusHistory>().WithMany().HasForeignKey(x => x.RequestStatusHistoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RequestMessage>().WithMany().HasForeignKey(x => x.ResponseMessageId).OnDelete(DeleteBehavior.Restrict);
    }
}
