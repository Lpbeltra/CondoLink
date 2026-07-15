using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Condominium> Condominiums => Set<Condominium>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<CondominiumMembership> CondominiumMemberships =>
        Set<CondominiumMembership>();

    public DbSet<CondominiumMembershipRole> CondominiumMembershipRoles =>
        Set<CondominiumMembershipRole>();

    public DbSet<UnitMembership> UnitMemberships => Set<UnitMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
