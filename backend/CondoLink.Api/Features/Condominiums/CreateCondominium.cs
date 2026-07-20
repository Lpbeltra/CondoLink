using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Condominiums;

public static class CreateCondominium
{
    public static IEndpointRouteBuilder MapCreateCondominium(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/condominiums", HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authenticatedUserIdValue =
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(
                authenticatedUserIdValue,
                out var authenticatedUserId))
        {
            return Results.Json(
                new { error = "Invalid authenticated user." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var authenticatedUser = await dbContext
            .Set<ApplicationUser>()
            .Where(user => user.Id == authenticatedUserId)
            .SingleOrDefaultAsync(cancellationToken);

        if (authenticatedUser is null)
        {
            return Results.Json(
                new { error = "Authenticated user was not found." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!authenticatedUser.IsActive)
        {
            return Results.Json(
                new { error = "User account is inactive." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(
                new { error = "Name is required." });
        }

        var name = request.Name.Trim();

        if (name.Length > 200)
        {
            return Results.BadRequest(
                new { error = "Name must not exceed 200 characters." });
        }

        var email = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim();

        if (email is not null
            && !new EmailAddressAttribute().IsValid(email))
        {
            return Results.BadRequest(
                new { error = "Email is invalid." });
        }

        if (email?.Length > 254)
        {
            return Results.BadRequest(
                new { error = "Email must not exceed 254 characters." });
        }

        var phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();

        if (phoneNumber?.Length > 30)
        {
            return Results.BadRequest(
                new
                {
                    error = "PhoneNumber must not exceed 30 characters."
                });
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var condominium = new Condominium(
                name,
                email,
                phoneNumber);

            var membership = new CondominiumMembership(
                authenticatedUserId,
                condominium.Id);

            var managerRole = new CondominiumMembershipRole(
                membership.Id,
                CondominiumRole.Manager);

            dbContext.Condominiums.Add(condominium);
            dbContext.CondominiumMemberships.Add(membership);
            dbContext.CondominiumMembershipRoles.Add(managerRole);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var response = new Response(
                condominium.Id,
                condominium.Name,
                condominium.Email,
                condominium.PhoneNumber,
                condominium.IsActive,
                condominium.CreatedAt);

            return Results.Created(
                $"/condominiums/{condominium.Id}",
                response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public sealed record Request(
        string? Name,
        string? Email,
        string? PhoneNumber);

    public sealed record Response(
        Guid Id,
        string Name,
        string? Email,
        string? PhoneNumber,
        bool IsActive,
        DateTime CreatedAt);
}