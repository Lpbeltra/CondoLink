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
    public DbSet<ManagementCompany> ManagementCompanies =>
        Set<ManagementCompany>();
    public DbSet<ManagementCompanyEmployee> ManagementCompanyEmployees =>
        Set<ManagementCompanyEmployee>();
    public DbSet<ManagementCompanyRequestCategory>
        ManagementCompanyRequestCategories =>
        Set<ManagementCompanyRequestCategory>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<CondominiumBlock> CondominiumBlocks => Set<CondominiumBlock>();
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
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WhatsAppInboundMessage> WhatsAppInboundMessages =>
        Set<WhatsAppInboundMessage>();
    public DbSet<WhatsAppSession> WhatsAppSessions => Set<WhatsAppSession>();
    public DbSet<WhatsAppDraftAttachment> WhatsAppDraftAttachments =>
        Set<WhatsAppDraftAttachment>();
    public DbSet<WhatsAppOutboundMessage> WhatsAppOutboundMessages =>
        Set<WhatsAppOutboundMessage>();
    public DbSet<WhatsAppPhoneVerification> WhatsAppPhoneVerifications =>
        Set<WhatsAppPhoneVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        InvalidatePhoneVerifications();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await InvalidatePhoneVerificationsAsync(cancellationToken);
        return await base.SaveChangesAsync(
            acceptAllChangesOnSuccess, cancellationToken);
    }

    private void InvalidatePhoneVerifications()
    {
        var changes = ChangedPhoneUsers();
        if (changes.Count == 0) return;
        var now = DateTime.UtcNow;
        foreach (var userId in changes)
        {
            foreach (var verification in WhatsAppPhoneVerifications.Where(x =>
                         x.UserId == userId && x.ConfirmedAt == null
                         && x.InvalidatedAt == null))
                verification.Invalidate(now);
        }
    }

    private async Task InvalidatePhoneVerificationsAsync(
        CancellationToken cancellationToken)
    {
        var changes = ChangedPhoneUsers();
        if (changes.Count == 0) return;
        var now = DateTime.UtcNow;
        var verifications = await WhatsAppPhoneVerifications.Where(x =>
                changes.Contains(x.UserId) && x.ConfirmedAt == null
                && x.InvalidatedAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var verification in verifications)
            verification.Invalidate(now);
    }

    private HashSet<Guid> ChangedPhoneUsers() =>
        ChangeTracker.Entries<ApplicationUser>()
            .Where(entry => entry.State == EntityState.Modified
                && entry.Property(x => x.NormalizedPhoneNumber).IsModified
                && !string.Equals(
                    entry.Property(x => x.NormalizedPhoneNumber).OriginalValue,
                    entry.Property(x => x.NormalizedPhoneNumber).CurrentValue,
                    StringComparison.Ordinal))
            .Select(entry => entry.Entity.Id)
            .ToHashSet();
}
