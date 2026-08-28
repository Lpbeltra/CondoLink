using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public static class SetCondominiumManagementCompany
{
    public static IEndpointRouteBuilder MapSetCondominiumManagementCompany(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
                "/overwatch/condominiums/{condominiumId:guid}/management-company",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Set condominium management company");

        return endpoints;
    }

    /// <summary>Internal (not private) so Postgres-backed concurrency tests can call the real handler directly.</summary>
    internal static async Task<IResult> HandleAsync(
        Guid condominiumId,
        Request request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var condominium = await dbContext.Condominiums
            .FirstOrDefaultAsync(
                condominium => condominium.Id == condominiumId,
                cancellationToken);

        if (condominium is null)
        {
            return Results.NotFound(new
            {
                message = "Condominium not found."
            });
        }

        if (request.ManagementCompanyId.HasValue)
        {
            var companyExists = await dbContext.ManagementCompanies
                .AnyAsync(
                    company => company.Id == request.ManagementCompanyId.Value && company.IsActive,
                    cancellationToken);

            if (!companyExists)
            {
                return Results.NotFound(new
                {
                    message = "Management company not found."
                });
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({condominiumId.ToString()}, 7311));", cancellationToken);
        var activeLink = await dbContext.CondominiumManagementCompanyLinks
            .SingleOrDefaultAsync(x => x.CondominiumId == condominiumId && x.IsActive, cancellationToken);
        if (activeLink?.ManagementCompanyId != request.ManagementCompanyId)
        {
            activeLink?.Unlink(DateTime.UtcNow);
            if (request.ManagementCompanyId is Guid companyId)
                dbContext.CondominiumManagementCompanyLinks.Add(
                    new Domain.Entities.CondominiumManagementCompanyLink(condominiumId, companyId));
        }
        condominium.SetManagementCompany(request.ManagementCompanyId);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new Response(
            condominium.Id,
            condominium.Name,
            condominium.Email,
            condominium.Cnpj,
            condominium.ManagementCompanyId,
            condominium.IsActive,
            condominium.CreatedAt,
            condominium.UpdatedAt));
    }

    public sealed record Request(Guid? ManagementCompanyId);

    public sealed record Response(
        Guid Id,
        string Name,
        string? Email,
        string? Cnpj,
        Guid? ManagementCompanyId,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
