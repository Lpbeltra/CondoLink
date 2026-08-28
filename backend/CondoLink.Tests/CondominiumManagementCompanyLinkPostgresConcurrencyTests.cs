using CondoLink.Api.Features.Overwatch.ManagementCompanies;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

/// <summary>
/// Lote 6 — item 9: "at most one active ManagementCompany link per condominium" is
/// enforced by a filtered unique index plus a condominium-scoped advisory lock
/// (see SetCondominiumManagementCompany.HandleAsync), but had zero concurrency coverage.
/// Both concurrent calls target the same condominium, so the advisory lock fully
/// serializes them — the invariant under test is that the database always ends up
/// consistent (exactly one active link, matching Condominium.ManagementCompanyId)
/// regardless of which call's transaction commits last, and that an existing
/// ManagementCompanyRequest's historical ManagementCompanyId is never touched by either.
/// </summary>
public sealed class CondominiumManagementCompanyLinkPostgresConcurrencyTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("COMVY_TEST_POSTGRES");

    [Fact]
    public async Task Concurrent_management_company_changes_leave_exactly_one_active_link_and_never_alter_existing_requests()
    {
        if (Connection is null) return;
        var (condoId, companyAId, companyBId, existingRequestId) = await Seed();

        await Task.WhenAll(SetAsync(condoId, companyAId), SetAsync(condoId, companyBId));

        await using var db = Db();
        var activeLinks = await db.CondominiumManagementCompanyLinks
            .Where(x => x.CondominiumId == condoId && x.IsActive).ToListAsync();
        Assert.Single(activeLinks);

        var condominium = await db.Condominiums.SingleAsync(x => x.Id == condoId);
        Assert.Equal(activeLinks[0].ManagementCompanyId, condominium.ManagementCompanyId);
        Assert.True(condominium.ManagementCompanyId == companyAId || condominium.ManagementCompanyId == companyBId);

        var existingRequest = await db.ManagementCompanyRequests.SingleAsync(x => x.Id == existingRequestId);
        Assert.Equal(companyAId, existingRequest.ManagementCompanyId);
    }

    private static async Task<(Guid CondoId, Guid CompanyAId, Guid CompanyBId, Guid ExistingRequestId)> Seed()
    {
        await using var db = Db();
        var condo = new Condominium("Concorrência Vínculo", null, null);
        var companyA = new ManagementCompany("Empresa A", null, null, null, null);
        var companyB = new ManagementCompany("Empresa B", null, null, null, null);
        var creator = CoreTestSeed.User("Gestor", $"gestor-{Guid.NewGuid():N}@test.local");
        var categoryA = new ManagementCompanyRequestCategory(companyA.Id, "Dúvidas", null, ManagementCompanyRequestFormType.Generic);
        var existingRequest = new CondoLink.Domain.Entities.ManagementCompanyRequest(
            condo.Id, companyA.Id, categoryA.Id, creator.Id, ManagementCompanyRequestType.GeneralQuestion);
        db.AddRange(condo, companyA, companyB, creator, categoryA, existingRequest,
            new ManagementCompanyGeneralQuestionRequest(existingRequest.Id, "Tema"));
        await db.SaveChangesAsync();
        return (condo.Id, companyA.Id, companyB.Id, existingRequest.Id);
    }

    private static async Task SetAsync(Guid condominiumId, Guid managementCompanyId)
    {
        await using var db = Db();
        await SetCondominiumManagementCompany.HandleAsync(
            condominiumId, new SetCondominiumManagementCompany.Request(managementCompanyId), db, default);
    }

    private static AppDbContext Db()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection).Options;
        return new AppDbContext(options);
    }
}
