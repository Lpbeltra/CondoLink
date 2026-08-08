using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

public sealed class ProductionDataResetServiceTests
{
    [Fact]
    public async Task Dry_run_is_read_only_and_execute_preserves_only_platform_admin_identity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var admin = new ApplicationUser("Admin", "admin@example.com", "+55 11 99999-1000");
        var resident = new ApplicationUser("Resident", "resident@example.com", "+55 11 99999-2000");
        const string password = "StrongPass1";
        var hasher = new PasswordHasher<ApplicationUser>();
        admin.PasswordHash = hasher.HashPassword(admin, password);
        resident.PasswordHash = hasher.HashPassword(resident, password);
        admin.NormalizedEmail = "ADMIN@EXAMPLE.COM";
        admin.NormalizedUserName = "ADMIN@EXAMPLE.COM";
        resident.NormalizedEmail = "RESIDENT@EXAMPLE.COM";
        resident.NormalizedUserName = "RESIDENT@EXAMPLE.COM";
        var platformRole = new IdentityRole<Guid>(ProductionDataResetService.PlatformAdminRole)
        {
            Id = Guid.NewGuid(), NormalizedName = "PLATFORMADMIN"
        };
        var residentRole = new IdentityRole<Guid>("Resident")
        {
            Id = Guid.NewGuid(), NormalizedName = "RESIDENT"
        };
        db.AddRange(admin, resident, platformRole, residentRole);
        db.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = platformRole.Id },
            new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = residentRole.Id },
            new IdentityUserRole<Guid> { UserId = resident.Id, RoleId = residentRole.Id });

        var condominium = new Condominium("Condo", null, null);
        var unit = new Unit(condominium.Id, "101", null, null, null);
        var category = new Category(condominium.Id, "Maintenance", null);
        var membership = new CondominiumMembership(resident.Id, condominium.Id);
        var adminMembership = new CondominiumMembership(admin.Id, condominium.Id);
        var unitMembership = new UnitMembership(resident.Id, unit.Id,
            UnitRelationshipType.Owner, true, true);
        var request = new CondoLink.Domain.Entities.Request(condominium.Id, resident.Id,
            unit.Id, category.Id, "Leak", "There is a leak");
        var message = new RequestMessage(request.Id, resident.Id, "Details");
        var attachment = new RequestAttachment(request.Id, resident.Id, "photo.jpg",
            "requests/photo.jpg", "image/jpeg", 10, message.Id);
        var notification = new Notification(resident.Id, condominium.Id,
            NotificationType.RequestCreated, "Created", "Request created", request.Id);
        var session = new WhatsAppSession("5511999992000", DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30));
        session.ResolveContext(resident.Id, condominium.Id, unit.Id);
        var inbound = new WhatsAppInboundMessage("wamid.reset", "5511999992000",
            "text", "hello", DateTime.UtcNow);
        inbound.Complete(resident.Id, "main_menu", DateTime.UtcNow);
        var outbound = new WhatsAppOutboundMessage(request.Id, message.Id, resident.Id,
            condominium.Id, "5511999992000", WhatsAppNotificationType.AdministrationMessage,
            WhatsAppSendMode.SessionText, "reset-test", "created", null, null, DateTime.UtcNow);
        var draft = new WhatsAppDraftAttachment(session.Id, "media-reset", "photo.jpg",
            "draft/photo.jpg", "image/jpeg", 10);
        db.AddRange(condominium, unit, category, membership, adminMembership,
            unitMembership, request, message, attachment, notification, session,
            inbound, outbound, draft);
        await db.SaveChangesAsync();

        var service = new ProductionDataResetService(db);
        var dryRun = await service.RunAsync(" Admin@Example.com ", execute: false);
        Assert.False(dryRun.Executed);
        Assert.Equal(1, dryRun.Counts["users"]);
        Assert.Equal(1, dryRun.Counts["condominiums"]);
        Assert.Equal(1, dryRun.Counts["request_messages"]);
        Assert.Equal(1, dryRun.Counts["whatsapp_outbound_messages"]);
        Assert.Equal(2, await db.Set<ApplicationUser>().CountAsync());

        var passwordHash = admin.PasswordHash;
        var result = await service.RunAsync("admin@example.com", execute: true);
        Assert.True(result.Executed);
        db.ChangeTracker.Clear();

        var preserved = await db.Set<ApplicationUser>().SingleAsync();
        Assert.Equal(admin.Id, preserved.Id);
        Assert.Equal(passwordHash, preserved.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(preserved, preserved.PasswordHash!, password));
        Assert.Null(preserved.ActiveManagementCondominiumId);
        Assert.Equal([platformRole.Id], await db.UserRoles
            .Where(link => link.UserId == preserved.Id).Select(link => link.RoleId).ToArrayAsync());
        Assert.Equal(2, await db.Roles.CountAsync());
        Assert.Empty(await db.Condominiums.ToArrayAsync());
        Assert.Empty(await db.Units.ToArrayAsync());
        Assert.Empty(await db.CondominiumMemberships.ToArrayAsync());
        Assert.Empty(await db.UnitMemberships.ToArrayAsync());
        Assert.Empty(await db.Requests.ToArrayAsync());
        Assert.Empty(await db.RequestMessages.ToArrayAsync());
        Assert.Empty(await db.RequestAttachments.ToArrayAsync());
        Assert.Empty(await db.Notifications.ToArrayAsync());
        Assert.Empty(await db.WhatsAppSessions.ToArrayAsync());
        Assert.Empty(await db.WhatsAppInboundMessages.ToArrayAsync());
        Assert.Empty(await db.WhatsAppOutboundMessages.ToArrayAsync());
        Assert.Empty(await db.WhatsAppDraftAttachments.ToArrayAsync());

        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_key_check;");
    }
}
