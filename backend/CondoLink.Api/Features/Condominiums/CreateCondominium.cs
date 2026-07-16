using System.ComponentModel.DataAnnotations;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;

namespace CondoLink.Api.Features.Condominiums;

public static class CreateCondominium
{
    public static IEndpointRouteBuilder MapCreateCondominium(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/condominiums", HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "Name is required." });
        }

        if (request.Name.Trim().Length > 200)
        {
            return Results.BadRequest(new { error = "Name must not exceed 200 characters." });
        }

        var email = request.Email?.Trim();

        if (request.Email is not null && !new EmailAddressAttribute().IsValid(email))
        {
            return Results.BadRequest(new { error = "Email is invalid." });
        }

        if (email?.Length > 254)
        {
            return Results.BadRequest(new { error = "Email must not exceed 254 characters." });
        }

        if (request.PhoneNumber?.Trim().Length > 30)
        {
            return Results.BadRequest(new { error = "PhoneNumber must not exceed 30 characters." });
        }

        var condominium = new Condominium(request.Name, request.Email, request.PhoneNumber);

        dbContext.Condominiums.Add(condominium);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new Response(
            condominium.Id,
            condominium.Name,
            condominium.Email,
            condominium.PhoneNumber,
            condominium.IsActive,
            condominium.CreatedAt);

        return Results.Created($"/condominiums/{condominium.Id}", response);
    }

    public sealed record Request(string? Name, string? Email, string? PhoneNumber);

    public sealed record Response(
        Guid Id,
        string Name,
        string? Email,
        string? PhoneNumber,
        bool IsActive,
        DateTime CreatedAt);
}
