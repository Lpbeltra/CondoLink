using System.Collections.Concurrent;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Categories;

public sealed class RequestCategoryResolver(AppDbContext db)
{
    public const string OtherName = "Outros";
    private const string NormalizedOtherName = "OUTROS";
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> LocalLocks = new();

    public async Task<Category> ResolveForClassificationAsync(
        Guid condominiumId, string? suggestedName, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(suggestedName))
        {
            var normalized = suggestedName.Trim().ToUpperInvariant();
            var suggested = await db.Categories.SingleOrDefaultAsync(category =>
                category.CondominiumId == condominiumId
                && category.NormalizedName == normalized
                && category.IsActive, ct);
            if (suggested is not null) return suggested;
        }

        return await GetOrCreateOtherAsync(condominiumId, ct);
    }

    public async Task<Category> GetOrCreateOtherAsync(
        Guid condominiumId, CancellationToken ct)
    {
        var gate = LocalLocks.GetOrAdd(condominiumId,
            static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var existing = await db.Categories.SingleOrDefaultAsync(category =>
                category.CondominiumId == condominiumId
                && category.NormalizedName == NormalizedOtherName, ct);
            if (existing is not null)
            {
                if (!existing.IsActive)
                {
                    existing.Activate();
                    await db.SaveChangesAsync(ct);
                }
                return existing;
            }

            var created = new Category(condominiumId, OtherName, null);
            db.Categories.Add(created);
            try
            {
                await db.SaveChangesAsync(ct);
                return created;
            }
            catch (DbUpdateException)
            {
                db.Entry(created).State = EntityState.Detached;
                return await db.Categories.SingleAsync(category =>
                    category.CondominiumId == condominiumId
                    && category.NormalizedName == NormalizedOtherName, ct);
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
