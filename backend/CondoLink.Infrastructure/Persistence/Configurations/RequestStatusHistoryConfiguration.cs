using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class RequestStatusHistoryConfiguration
    : IEntityTypeConfiguration<RequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<RequestStatusHistory> builder)
    {
        builder.ToTable("request_status_history");
        builder.HasKey(history => history.Id);

        builder.Property(history => history.Id).HasColumnName("id");
        builder.Property(history => history.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(history => history.PreviousStatus).HasColumnName("previous_status").HasConversion<int?>();
        builder.Property(history => history.NewStatus).HasColumnName("new_status").HasConversion<int>().IsRequired();
        builder.Property(history => history.ChangedByUserId).HasColumnName("changed_by_user_id").IsRequired();
        builder.Property(history => history.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(history => history.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<DomainRequest>().WithMany().HasForeignKey(history => history.RequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(history => history.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(history => new { history.RequestId, history.CreatedAt });
    }
}
