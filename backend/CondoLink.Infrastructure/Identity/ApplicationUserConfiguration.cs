using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Identity;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public const string UniqueNormalizedEmailIndex = "ux_users_normalized_email";
    public const string UniqueNormalizedPhoneNumberIndex =
        "ux_users_normalized_phone_number";

    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.Property(user => user.Id).HasColumnName("id");
        builder.Property(user => user.UserName).HasColumnName("user_name").HasMaxLength(254).IsRequired();
        builder.Property(user => user.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(254).IsRequired();
        builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(254).IsRequired();
        builder.Property(user => user.EmailConfirmed).HasColumnName("email_confirmed");
        builder.Property(user => user.PasswordHash).HasColumnName("password_hash");
        builder.Property(user => user.SecurityStamp).HasColumnName("security_stamp");
        builder.Property(user => user.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        builder.Property(user => user.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30);
        builder.Property(user => user.NormalizedPhoneNumber)
            .HasColumnName("normalized_phone_number")
            .HasMaxLength(16);
        builder.Property(user => user.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
        builder.Property(user => user.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        builder.Property(user => user.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(user => user.LockoutEnabled).HasColumnName("lockout_enabled");
        builder.Property(user => user.AccessFailedCount).HasColumnName("access_failed_count");

        builder.Property(user => user.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(user => user.Cpf).HasColumnName("cpf").HasMaxLength(11);
        builder.Property(user => user.Cnpj).HasColumnName("cnpj").HasMaxLength(14);
        builder.Property(user => user.Address).HasColumnName("address").HasMaxLength(200);
        builder.Property(user => user.City).HasColumnName("city").HasMaxLength(100);
        builder.Property(user => user.State).HasColumnName("state").HasMaxLength(2);

        builder.Property(user => user.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(user => user.MustChangePassword)
            .HasColumnName("must_change_password")
            .IsRequired();
        builder.Property(user => user.ReceiveWhatsAppUpdates)
            .HasColumnName("receive_whatsapp_updates").HasDefaultValue(true).IsRequired();

        builder.Property(user => user.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(user => user.PasswordChangedAt)
            .HasColumnName("password_changed_at");

        builder.Property(user => user.ActiveManagementCondominiumId)
            .HasColumnName("active_management_condominium_id");    

        builder.Property(user => user.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName(UniqueNormalizedEmailIndex)
            .IsUnique();
        builder.HasIndex(user => user.NormalizedPhoneNumber)
            .HasDatabaseName(UniqueNormalizedPhoneNumberIndex)
            .IsUnique()
            .HasFilter("\"normalized_phone_number\" IS NOT NULL");
        builder.HasIndex(user => user.Cpf)
            .HasDatabaseName("ux_users_manager_cpf")
            .IsUnique()
            .HasFilter("\"cpf\" IS NOT NULL");
        builder.HasIndex(user => user.Cnpj)
            .HasDatabaseName("ux_users_manager_cnpj")
            .IsUnique()
            .HasFilter("\"cnpj\" IS NOT NULL");
    }
}
