using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public const string UniqueRegistrationNumberIndex = "ux_employees_condominium_id_registration_number";

    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");
        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.Id).HasColumnName("id");
        builder.Property(employee => employee.CondominiumId).HasColumnName("condominium_id").IsRequired();
        builder.Property(employee => employee.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(employee => employee.JobTitle).HasColumnName("job_title").HasMaxLength(120);
        builder.Property(employee => employee.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
        builder.Property(employee => employee.NormalizedPhoneNumber).HasColumnName("normalized_phone_number").HasMaxLength(32);
        builder.Property(employee => employee.Email).HasColumnName("email").HasMaxLength(256);
        builder.Property(employee => employee.RegistrationNumber).HasColumnName("registration_number").HasMaxLength(60);
        builder.Property(employee => employee.NormalizedRegistrationNumber).HasColumnName("normalized_registration_number").HasMaxLength(60);
        builder.Property(employee => employee.AdmissionDate).HasColumnName("admission_date");
        builder.Property(employee => employee.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(employee => employee.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(employee => employee.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<Condominium>().WithMany().HasForeignKey(employee => employee.CondominiumId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(employee => employee.CondominiumId).HasDatabaseName("ix_employees_condominium_id");
        builder.HasIndex(employee => new { employee.CondominiumId, employee.NormalizedRegistrationNumber })
            .HasDatabaseName(UniqueRegistrationNumberIndex)
            .IsUnique()
            .HasFilter("normalized_registration_number IS NOT NULL");
    }
}
