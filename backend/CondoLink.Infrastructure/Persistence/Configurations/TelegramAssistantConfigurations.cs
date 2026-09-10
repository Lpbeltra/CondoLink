using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class TelegramUserLinkConfiguration : IEntityTypeConfiguration<TelegramUserLink>
{
    public void Configure(EntityTypeBuilder<TelegramUserLink> b)
    { b.ToTable("telegram_user_links"); b.HasKey(x => x.Id); b.HasIndex(x => x.UserId).IsUnique();
      b.HasIndex(x => x.TelegramUserId).IsUnique(); b.HasIndex(x => x.TelegramChatId).IsUnique();
      b.HasOne<CondoLink.Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
      b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.ActiveCondominiumId).OnDelete(DeleteBehavior.SetNull); }
}
public sealed class TelegramLinkCodeConfiguration : IEntityTypeConfiguration<TelegramLinkCode>
{
    public void Configure(EntityTypeBuilder<TelegramLinkCode> b)
    { b.ToTable("telegram_link_codes"); b.HasKey(x => x.Id); b.Property(x => x.CodeHash).HasMaxLength(64);
      b.HasIndex(x => x.CodeHash).IsUnique(); b.HasIndex(x => new { x.UserId, x.ExpiresAt });
      b.HasOne<CondoLink.Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class TelegramInboundUpdateConfiguration : IEntityTypeConfiguration<TelegramInboundUpdate>
{
    public void Configure(EntityTypeBuilder<TelegramInboundUpdate> b)
    { b.ToTable("telegram_inbound_updates"); b.HasKey(x => x.Id); b.HasIndex(x => x.UpdateId).IsUnique();
      b.Property(x => x.Text).HasMaxLength(4096);
      b.Property(x => x.LastError).HasMaxLength(100); b.Property(x => x.Status).HasConversion<int>();
      b.HasIndex(x => new { x.Status, x.NextAttemptAt }); b.HasIndex(x => new { x.ChatId, x.ReceivedAt }); }
}
