using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppSessionConfiguration : IEntityTypeConfiguration<WhatsAppSession>
{
    public void Configure(EntityTypeBuilder<WhatsAppSession> builder)
    {
        builder.ToTable("whatsapp_sessions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
        builder.Property(item => item.UserId).HasColumnName("user_id");
        builder.Property(item => item.CondominiumId).HasColumnName("condominium_id");
        builder.Property(item => item.UnitId).HasColumnName("unit_id");
        builder.Property(item => item.RequestId).HasColumnName("request_id");
        builder.Property(item => item.RequestClosureConfirmationId).HasColumnName("request_closure_confirmation_id");
        builder.Property(item => item.CategoryId).HasColumnName("category_id");
        builder.Property(item => item.DraftDescription).HasColumnName("draft_description").HasMaxLength(4000);
        builder.Property(item => item.DraftAiProposalJson).HasColumnName("draft_ai_proposal_json").HasMaxLength(12000);
        builder.Property(item => item.Page).HasColumnName("page").IsRequired();
        builder.Property(item => item.State).HasColumnName("state").HasConversion<int>().IsRequired();
        builder.Property(item => item.PreviousState).HasColumnName("previous_state").HasConversion<int?>();
        builder.Property(item => item.LastInteractionAt).HasColumnName("last_interaction_at").IsRequired();
        builder.Property(item => item.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.HasIndex(item => item.PhoneNumber).HasDatabaseName("ux_whatsapp_sessions_phone_number").IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Condominium>().WithMany().HasForeignKey(item => item.CondominiumId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Unit>().WithMany().HasForeignKey(item => item.UnitId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Request>().WithMany().HasForeignKey(item => item.RequestId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<RequestClosureConfirmation>().WithMany()
            .HasForeignKey(item => item.RequestClosureConfirmationId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.SetNull);
    }
}
