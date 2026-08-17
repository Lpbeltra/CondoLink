using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Infrastructure.Persistence;

public sealed record ProductionDataResetResult(
    bool Executed,
    IReadOnlyDictionary<string, int> Counts);

public sealed class ProductionDataResetService(AppDbContext db)
{
    public const string PlatformAdminRole = "PlatformAdmin";

    public async Task<ProductionDataResetResult> RunAsync(
        string preserveUserEmail, bool execute, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(preserveUserEmail))
            throw new ArgumentException("The preserved user email is required.", nameof(preserveUserEmail));

        var normalizedEmail = preserveUserEmail.Trim().ToUpperInvariant();
        var users = await db.Set<ApplicationUser>()
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .Take(2).ToArrayAsync(cancellationToken);
        if (users.Length == 0)
            throw new InvalidOperationException("The preserved user was not found.");
        if (users.Length != 1)
            throw new InvalidOperationException("The preserved user email matched more than one user.");

        var preservedUser = users[0];
        var platformRole = await db.Roles
            .SingleOrDefaultAsync(role => role.NormalizedName == PlatformAdminRole.ToUpperInvariant(), cancellationToken)
            ?? throw new InvalidOperationException("The PlatformAdmin role does not exist.");
        if (!await db.UserRoles.AnyAsync(link =>
                link.UserId == preservedUser.Id && link.RoleId == platformRole.Id, cancellationToken))
            throw new InvalidOperationException("The preserved user does not have the PlatformAdmin role.");

        var counts = await CountAsync(preservedUser.Id, platformRole.Id, cancellationToken);
        if (!execute)
            return new ProductionDataResetResult(false, counts);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.WhatsAppDraftAttachments.ExecuteDeleteAsync(cancellationToken);
            await db.WhatsAppOutboundMessages.ExecuteDeleteAsync(cancellationToken);
            await db.RequestClosureConfirmations.ExecuteDeleteAsync(cancellationToken);
            await db.RequestResidentReplyRequirements.ExecuteDeleteAsync(cancellationToken);
            await db.RequestAiAnalyses.ExecuteDeleteAsync(cancellationToken);
            await db.RequestAttachments.ExecuteDeleteAsync(cancellationToken);
            await db.Notifications.ExecuteDeleteAsync(cancellationToken);
            await db.RequestMessages.ExecuteDeleteAsync(cancellationToken);
            await db.RequestStatusHistories.ExecuteDeleteAsync(cancellationToken);
            await db.WhatsAppSessions.ExecuteDeleteAsync(cancellationToken);
            await db.WhatsAppInboundMessages.ExecuteDeleteAsync(cancellationToken);
            await db.WhatsAppPhoneVerifications.ExecuteDeleteAsync(cancellationToken);
            await db.Requests.ExecuteDeleteAsync(cancellationToken);
            await db.UnitMemberships.ExecuteDeleteAsync(cancellationToken);
            await db.CondominiumMembershipRoles.ExecuteDeleteAsync(cancellationToken);
            await db.CondominiumMemberships.ExecuteDeleteAsync(cancellationToken);
            await db.ManagementCompanyEmployees.ExecuteDeleteAsync(cancellationToken);
            await db.ManagementCompanyRequestCategories.ExecuteDeleteAsync(cancellationToken);
            await db.Categories.ExecuteDeleteAsync(cancellationToken);
            await db.Units.ExecuteDeleteAsync(cancellationToken);
            await db.CondominiumBlocks.ExecuteDeleteAsync(cancellationToken);
            await db.Set<ApplicationUser>().Where(user => user.Id == preservedUser.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(user => user.ActiveManagementCondominiumId, (Guid?)null), cancellationToken);
            await db.Condominiums.ExecuteDeleteAsync(cancellationToken);
            await db.ManagementCompanies.ExecuteDeleteAsync(cancellationToken);
            await db.UserRoles.Where(link => link.UserId == preservedUser.Id && link.RoleId != platformRole.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db.Set<ApplicationUser>().Where(user => user.Id != preservedUser.Id)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new ProductionDataResetResult(true, counts);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<IReadOnlyDictionary<string, int>> CountAsync(
        Guid preservedUserId, Guid platformRoleId, CancellationToken ct) =>
        new SortedDictionary<string, int>(StringComparer.Ordinal)
        {
            ["users"] = await db.Set<ApplicationUser>().CountAsync(user => user.Id != preservedUserId, ct),
            ["non_platform_admin_assignments_of_preserved_user"] = await db.UserRoles.CountAsync(
                link => link.UserId == preservedUserId && link.RoleId != platformRoleId, ct),
            ["condominiums"] = await db.Condominiums.CountAsync(ct),
            ["management_companies"] = await db.ManagementCompanies.CountAsync(ct),
            ["management_company_employees"] = await db.ManagementCompanyEmployees.CountAsync(ct),
            ["management_company_request_categories"] = await db.ManagementCompanyRequestCategories.CountAsync(ct),
            ["condominium_memberships"] = await db.CondominiumMemberships.CountAsync(ct),
            ["condominium_membership_roles"] = await db.CondominiumMembershipRoles.CountAsync(ct),
            ["blocks"] = await db.CondominiumBlocks.CountAsync(ct),
            ["units"] = await db.Units.CountAsync(ct),
            ["unit_memberships"] = await db.UnitMemberships.CountAsync(ct),
            ["categories"] = await db.Categories.CountAsync(ct),
            ["requests"] = await db.Requests.CountAsync(ct),
            ["request_status_histories"] = await db.RequestStatusHistories.CountAsync(ct),
            ["request_messages"] = await db.RequestMessages.CountAsync(ct),
            ["request_attachments"] = await db.RequestAttachments.CountAsync(ct),
            ["request_ai_analyses"] = await db.RequestAiAnalyses.CountAsync(ct),
            ["request_resident_reply_requirements"] = await db.RequestResidentReplyRequirements.CountAsync(ct),
            ["request_closure_confirmations"] = await db.RequestClosureConfirmations.CountAsync(ct),
            ["notifications"] = await db.Notifications.CountAsync(ct),
            ["whatsapp_sessions"] = await db.WhatsAppSessions.CountAsync(ct),
            ["whatsapp_draft_attachments"] = await db.WhatsAppDraftAttachments.CountAsync(ct),
            ["whatsapp_inbound_messages"] = await db.WhatsAppInboundMessages.CountAsync(ct),
            ["whatsapp_outbound_messages"] = await db.WhatsAppOutboundMessages.CountAsync(ct),
            ["whatsapp_phone_verifications"] = await db.WhatsAppPhoneVerifications.CountAsync(ct)
        };
}
