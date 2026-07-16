using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class RequestMessageConfiguration
    : IEntityTypeConfiguration<RequestMessage>
{
    public void Configure(EntityTypeBuilder<RequestMessage> builder)
    {
        builder.ToTable("request_messages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id");

        builder.Property(message => message.RequestId)
            .HasColumnName("request_id")
            .IsRequired();

        builder.Property(message => message.AuthorUserId)
            .HasColumnName("author_user_id")
            .IsRequired();

        builder.Property(message => message.Content)
            .HasColumnName("content")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(message => message.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne<DomainRequest>()
            .WithMany()
            .HasForeignKey(message => message.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(message => message.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(message => new
            {
                message.RequestId,
                message.CreatedAt
            });
    }
}
