using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Condominium> Condominiums => Set<Condominium>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<CondominiumMembership> CondominiumMemberships =>
        Set<CondominiumMembership>();
    public DbSet<CondominiumMembershipRole> CondominiumMembershipRoles =>
        Set<CondominiumMembershipRole>();
    public DbSet<UnitMembership> UnitMemberships => Set<UnitMembership>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<DomainRequest> Requests => Set<DomainRequest>();
    public DbSet<RequestStatusHistory> RequestStatusHistories =>
        Set<RequestStatusHistory>();
    public DbSet<RequestMessage> RequestMessages => Set<RequestMessage>();
    public DbSet<RequestAttachment> RequestAttachments => Set<RequestAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
