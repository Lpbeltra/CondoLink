using System.Security.Cryptography;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CondoLink.Domain.Enums;

namespace CondoLink.Api.Features.Overwatch.Managers;

public static class CreateOverwatchManager
{
    private const string ManagerRoleName = "Manager";

    public static IEndpointRouteBuilder MapCreateOverwatchManager(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/overwatch/managers",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Create manager")
            .WithDescription("Creates a new condominium manager.");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new
            {
                message = "Full name and email are required."
            });
        }

        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var validationError = ManagerValidation.Validate(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }
        var cpf = Domain.RegistrationData.Digits(request.Cpf);
        var cnpj = Domain.RegistrationData.Digits(request.Cnpj);
        var conflict = await ManagerValidation.FindConflictAsync(
            dbContext, cpf, cnpj,
            Domain.PhoneNumberNormalizer.Normalize(request.PhoneNumber),
            null, cancellationToken);
        if (conflict is not null) return Results.Conflict(new { message = conflict });

        var existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return Results.Conflict(new
            {
                message = "A user with this email already exists."
            });
        }

        if (!await roleManager.RoleExistsAsync(ManagerRoleName))
        {
            var createRoleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>(ManagerRoleName));

            if (!createRoleResult.Succeeded)
            {
                return Results.BadRequest(new
                {
                    errors = createRoleResult.Errors
                        .Select(error => error.Description)
                });
            }
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var user = new ApplicationUser(fullName, email, request.PhoneNumber);
        user.UpdateManagerProfile(fullName, request.PhoneNumber, cpf, cnpj,
            request.Address, request.City, request.State);
        user.SetPix(request.PixKeyType, request.PixKey);
        user.RequirePasswordChange();

        var createResult = await userManager.CreateAsync(
            user,
            temporaryPassword);

        if (!createResult.Succeeded)
        {
            return Results.BadRequest(new
            {
                errors = createResult.Errors
                    .Select(error => error.Description)
            });
        }

        var roleResult = await userManager.AddToRoleAsync(
            user,
            ManagerRoleName);

        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            return Results.BadRequest(new
            {
                errors = roleResult.Errors
                    .Select(error => error.Description)
            });
        }

        return Results.Created(
            $"/overwatch/managers/{user.Id}",
            new ManagerCreatedResponse(
                user.Id,
                user.FullName,
                user.Email!,
                user.PhoneNumber,
                user.Cpf,
                user.Cnpj,
                user.Address,
                user.City,
                user.State,
                user.PixKeyType,
                user.PixKey,
                user.IsActive,
                0,
                user.CreatedAt,
                user.UpdatedAt,
                temporaryPassword));
    }

    private static string GenerateTemporaryPassword()
    {
        const string characters =
            "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(12);
        var randomPart = new string(
            bytes.Select(value => characters[value % characters.Length])
                .ToArray());
        return $"Aa1!{randomPart}";
    }

    public sealed record Request(
        string? FullName, string? Email, string? PhoneNumber, string? Cpf,
        string? Cnpj, string? Address, string? City, string? State,
        PixKeyType? PixKeyType = null, string? PixKey = null);

    public sealed record ManagerCreatedResponse(
        Guid Id,
        string FullName,
        string Email,
        string? PhoneNumber,
        string? Cpf,
        string? Cnpj,
        string? Address,
        string? City,
        string? State,
        PixKeyType? PixKeyType,
        string? PixKey,
        bool IsActive,
        int CondominiumCount,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string TemporaryPassword);
}

internal static class ManagerValidation
{
    public static string? Validate(CreateOverwatchManager.Request request)
    {
        if (request.FullName!.Trim().Length > 200 || request.Email!.Trim().Length > 254)
            return "Full name or email exceeds the allowed length.";
        if (request.PhoneNumber?.Trim().Length > 30) return "Phone number must not exceed 30 characters.";
        if (!Domain.PhoneNumberNormalizer.IsValidOptional(request.PhoneNumber))
            return "Phone number must be valid; include + and the country code outside Brazil.";
        if (request.Cpf is not null && !Domain.RegistrationData.IsValidCpf(request.Cpf)) return "CPF is invalid.";
        if (request.Cnpj is not null && !Domain.RegistrationData.IsValidCnpj(request.Cnpj)) return "CNPJ is invalid.";
        if (request.Address?.Trim().Length > 200) return "Address must not exceed 200 characters.";
        if (request.City?.Trim().Length > 100) return "City must not exceed 100 characters.";
        var state = Domain.RegistrationData.State(request.State);
        if (state is not null && !Domain.RegistrationData.IsValidState(state)) return "State is invalid.";
        try
        {
            var probe = new ApplicationUser("PIX validation", "pix-validation@example.test", null);
            probe.SetPix(request.PixKeyType, request.PixKey);
        }
        catch (ArgumentException exception) { return exception.Message; }
        return null;
    }

    public static async Task<string?> FindConflictAsync(
        AppDbContext db, string? cpf, string? cnpj,
        string? normalizedPhoneNumber, Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (cpf is not null && await db.Users.AnyAsync(x =>
            (!excludedId.HasValue || x.Id != excludedId) && x.Cpf == cpf, cancellationToken))
            return "A manager with this CPF already exists.";
        if (cnpj is not null && await db.Users.AnyAsync(x =>
            (!excludedId.HasValue || x.Id != excludedId) && x.Cnpj == cnpj, cancellationToken))
            return "A manager with this CNPJ already exists.";
        if (normalizedPhoneNumber is not null && await db.Users.AnyAsync(x =>
            (!excludedId.HasValue || x.Id != excludedId)
            && x.NormalizedPhoneNumber == normalizedPhoneNumber,
            cancellationToken))
            return "A user with this phone number already exists.";
        return null;
    }
}
