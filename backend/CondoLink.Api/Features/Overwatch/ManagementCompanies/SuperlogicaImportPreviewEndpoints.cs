using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CondoLink.Domain;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public static class SuperlogicaImportPreviewEndpoints
{
    private const int PageSize = 50;
    private const int MaximumPages = 1000;
    private const string Provider = "Superlogica";

    public static IEndpointRouteBuilder MapSuperlogicaImportPreviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/condominiums/{condominiumId:guid}/integrations/superlogica/import-preview", GetAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch")
            .WithSummary("Preview Superlogica condominium structure");
        return endpoints;
    }

    private static async Task<IResult> GetAsync(Guid condominiumId, AppDbContext db,
        IDataProtectionProvider protection, ISuperlogicaClient client, CancellationToken ct)
    {
        var condominium = await db.Condominiums.AsNoTracking().SingleOrDefaultAsync(x => x.Id == condominiumId, ct);
        if (condominium is null) return Results.NotFound(new { message = "Condomínio Comvy não encontrado." });
        if (condominium.ManagementCompanyId is not Guid administratorId)
            return Results.Conflict(new { message = "Condomínio precisa estar vinculado a uma administradora antes da consulta." });
        var mapping = await db.ExternalCondominiumMappings.AsNoTracking()
            .Where(x => x.CondominiumId == condominiumId && x.AdministratorIntegration.Provider == Provider
                && x.AdministratorIntegration.AdministratorId == administratorId)
            .Select(x => new
            {
                x.ExternalCondominiumId,
                x.AdministratorIntegration.EncryptedAppToken,
                x.AdministratorIntegration.EncryptedAccessToken,
                x.AdministratorIntegration.Status,
                AdministratorActive = x.AdministratorIntegration.Administrator.IsActive
            }).SingleOrDefaultAsync(ct);
        if (mapping is null)
            return Results.Conflict(new { message = "Vincule este condomínio à Superlógica no Overwatch antes de preparar a importação." });
        if (!mapping.AdministratorActive)
            return Results.Conflict(new { message = "A administradora desta integração está inativa. Revise a administradora no Overwatch." });
        if (mapping.Status is "Invalid" or "NotConfigured")
            return Results.BadRequest(new { code = "invalid_credentials", message = "Integração Superlógica inválida. Revise as credenciais no Overwatch." });

        try
        {
            var protector = protection.CreateProtector("Comvy.AdministratorIntegration.Superlogica.v1");
            var appToken = protector.Unprotect(mapping.EncryptedAppToken);
            var accessToken = protector.Unprotect(mapping.EncryptedAccessToken);
            var rows = new List<SuperlogicaUnitRow>();
            var seenPageSignatures = new HashSet<string>(StringComparer.Ordinal);
            for (var page = 1; page <= MaximumPages; page++)
            {
                var result = await client.ListUnitsPageAsync(appToken, accessToken, mapping.ExternalCondominiumId, page, ct);
                if (result.InvalidCredentials)
                    return Results.BadRequest(new { code = "invalid_credentials", message = "Credenciais inválidas. Revise a integração da Superlógica no Overwatch." });
                if (result.Rows is null) return ExternalFailure();
                if (result.Rows.Count == 0) break;
                var signature = PageSignature(result.Rows);
                if (!seenPageSignatures.Add(signature)) return ExternalFailure();
                rows.AddRange(result.Rows);
                if (result.Rows.Count < PageSize) break;
                if (page == MaximumPages) return ExternalFailure();
            }

            var blockRows = await db.CondominiumBlocks.AsNoTracking().Where(x => x.CondominiumId == condominiumId).ToListAsync(ct);
            var unitRows = await db.Units.AsNoTracking().Where(x => x.CondominiumId == condominiumId).ToListAsync(ct);
            var external = SuperlogicaImportPreviewBuilder.GetIdentityKeys(rows);
            var currentUnits = unitRows.Select(x => x.Id).ToArray();
            var matchingUsers = await FindMatchingUsersAsync(db, external, ct);
            var memberRows = await (from membership in db.CondominiumMemberships.AsNoTracking()
                join role in db.CondominiumMembershipRoles.AsNoTracking() on membership.Id equals role.CondominiumMembershipId
                join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
                where membership.CondominiumId == condominiumId && membership.IsActive && membership.EndedAt == null
                    && role.Role == CondominiumRole.Resident && role.IsActive && role.RevokedAt == null
                select new CondominiumMemberCandidate(user.Id, user.FullName)).Distinct().ToListAsync(ct);
            var unitLinks = currentUnits.Length == 0 ? [] : await db.UnitMemberships.AsNoTracking()
                .Where(x => currentUnits.Contains(x.UnitId) && x.IsActive && x.EndedAt == null).ToListAsync(ct);
            var preview = SuperlogicaImportPreviewBuilder.Build(mapping.ExternalCondominiumId, rows,
                blockRows, unitRows, matchingUsers, memberRows, unitLinks);
            return Results.Ok(preview);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return ExternalFailure(); }
        catch (HttpRequestException) { return ExternalFailure(); }
        catch (JsonException) { return ExternalFailure(); }
        catch (CryptographicException) { return Results.BadRequest(new { code = "invalid_credentials", message = "Integração Superlógica inválida. Revise as credenciais no Overwatch." }); }
    }

    private static async Task<List<ApplicationUser>> FindMatchingUsersAsync(AppDbContext db,
        SuperlogicaIdentityKeys keys, CancellationToken ct)
    {
        if (keys.Emails.Length == 0 && keys.Phones.Length == 0 && keys.TaxIds.Length == 0) return [];
        return await db.Users.AsNoTracking().Where(user =>
            keys.Emails.Contains(user.Email!) || keys.Phones.Contains(user.NormalizedPhoneNumber!) || keys.TaxIds.Contains(user.Cpf!))
            .ToListAsync(ct);
    }

    private static string PageSignature(IReadOnlyList<SuperlogicaUnitRow> rows)
    {
        var material = string.Join('\n', rows.Select(x => $"{x.ExternalUnitId}|{x.ExternalContactId}|{x.ExternalOwnerId}|{x.RelationshipType}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static IResult ExternalFailure() => Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}
