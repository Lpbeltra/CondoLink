using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ServiceProviders;

public static class OverwatchServiceProviderEndpoints
{
    public static IEndpointRouteBuilder MapOverwatchServiceProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/overwatch/service-providers").RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}/deletion-impact", ImpactAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(string? search, AppDbContext db, CancellationToken ct)
    {
        var term = search?.Trim().ToLowerInvariant();
        var providers = db.ServiceProviders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(term)) providers = providers.Where(x => x.Name.ToLower().Contains(term) || x.Phone.ToLower().Contains(term) || x.Specialty.ToLower().Contains(term) || db.ServiceProviderSpecialties.Any(s => s.ServiceProviderId == x.Id && s.Name.ToLower().Contains(term)));
        var rows = await providers.OrderBy(x => x.Name).ToArrayAsync(ct); var ids = rows.Select(x => x.Id).ToArray();
        var specialties = await db.ServiceProviderSpecialties.AsNoTracking().Where(x => ids.Contains(x.ServiceProviderId)).OrderBy(x => x.Name).ToArrayAsync(ct);
        var personal = await (from link in db.ServiceProviderUserLinks.AsNoTracking() join user in db.Users.AsNoTracking() on link.UserId equals user.Id where ids.Contains(link.ServiceProviderId) select new { link.ServiceProviderId, user.FullName }).ToArrayAsync(ct);
        var condominiums = await (from link in db.ServiceProviderCondominiumLinks.AsNoTracking() join condo in db.Condominiums.AsNoTracking() on link.CondominiumId equals condo.Id where ids.Contains(link.ServiceProviderId) select new { link.ServiceProviderId, condo.Name }).ToArrayAsync(ct);
        return Results.Ok(rows.Select(x => new ProviderResponse(x.Id, x.Name, x.Phone, specialties.Where(s => s.ServiceProviderId == x.Id).Select(s => s.Name).ToArray(), x.IsActive, personal.Where(p => p.ServiceProviderId == x.Id).Select(p => p.FullName).ToArray(), condominiums.Where(c => c.ServiceProviderId == x.Id).Select(c => c.Name).ToArray(), x.CreatedAt)));
    }

    private static async Task<IResult> ImpactAsync(Guid id, AppDbContext db, CancellationToken ct) => (await GetImpactAsync(id, db, ct)) is { } impact ? Results.Ok(impact) : Results.NotFound(new { message = "Prestador não encontrado." });

    private static async Task<IResult> DeleteAsync(Guid id, [FromBody] DeleteRequest input, ClaimsPrincipal principal, AppDbContext db, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        if (!string.Equals(input.Confirmation, "EXCLUIR PERMANENTEMENTE", StringComparison.Ordinal)) return Results.BadRequest(new { message = "Confirmação inválida." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var provider = await db.ServiceProviders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (provider is null) return Results.NotFound(new { message = "Prestador não encontrado." });
        // Request and payment FKs are SET NULL; request history and payment fields are immutable snapshots.
        db.ServiceProviders.Remove(provider);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "O prestador mudou e não pode ser excluído com segurança. Atualize a tela e tente novamente." }); }
        var actor = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        loggerFactory.CreateLogger(typeof(OverwatchServiceProviderEndpoints)).LogInformation("PlatformAdmin permanently deleted service provider. ActorUserId: {ActorUserId}; ServiceProviderId: {ServiceProviderId}; Name: {Name}; OccurredAt: {OccurredAt}", actor, id, provider.Name, DateTime.UtcNow);
        return Results.NoContent();
    }

    private static async Task<DeletionImpact?> GetImpactAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var name = await db.ServiceProviders.Where(x => x.Id == id).Select(x => x.Name).SingleOrDefaultAsync(ct);
        if (name is null) return null;
        // History intentionally has no master-provider FK; it preserves name/specialty snapshots.
        return new(await db.ServiceProviderUserLinks.CountAsync(x => x.ServiceProviderId == id, ct), await db.ServiceProviderCondominiumLinks.CountAsync(x => x.ServiceProviderId == id, ct), await db.Requests.CountAsync(x => x.ServiceProviderId == id, ct), await db.RequestServiceProviderHistories.CountAsync(x => x.ProviderName == name || x.PreviousName == name, ct), await db.ManagementCompanyPaymentRequests.CountAsync(x => x.ServiceProviderId == id, ct));
    }

    public sealed record ProviderResponse(Guid Id, string Name, string Phone, IReadOnlyList<string> Specialties, bool IsActive, IReadOnlyList<string> PersonalOwners, IReadOnlyList<string> Condominiums, DateTime CreatedAt);
    public sealed record DeletionImpact(int PersonalLinks, int CondominiumLinks, int CurrentRequests, int HistoricalRecords, int PaymentRequests);
    public sealed record DeleteRequest(string? Confirmation);
}
