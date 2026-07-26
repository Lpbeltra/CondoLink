using System.Security.Cryptography;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;

public static class CreateManagementCompanyEmployee
{
    public static IEndpointRouteBuilder MapCreateManagementCompanyEmployee(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/overwatch/management-companies/{managementCompanyId:guid}/employees",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Create management company employee");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managementCompanyId,
        Request request,
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var companyExists = await dbContext.ManagementCompanies
            .AnyAsync(
                company => company.Id == managementCompanyId,
                cancellationToken);
        if (!companyExists)
        {
            return Results.NotFound(new
            {
                message = "Management company not found."
            });
        }

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
        if (fullName.Length > 200 || email.Length > 254)
        {
            return Results.BadRequest(new
            {
                message = "Full name or email exceeds the allowed length."
            });
        }

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            var alreadyLinked = await dbContext.ManagementCompanyEmployees
                .AnyAsync(
                    employee => employee.UserId == existingUser.Id,
                    cancellationToken);
            return alreadyLinked
                ? Results.Conflict(new
                {
                    message =
                        "This user already belongs to a management company."
                })
                : Results.Conflict(new
                {
                    message = "A user with this email already exists."
                });
        }

        var temporaryPassword = GenerateTemporaryPassword();
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = new ApplicationUser(fullName, email, null);
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

        var employee = new ManagementCompanyEmployee(
            managementCompanyId,
            user.Id);
        dbContext.ManagementCompanyEmployees.Add(employee);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created(
            $"/employees/{employee.Id}",
            new CreatedResponse(
                employee.Id,
                employee.ManagementCompanyId,
                user.Id,
                user.FullName,
                user.Email!,
                employee.IsActive,
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

    public sealed record Request(string? FullName, string? Email);

    public sealed record CreatedResponse(
        Guid Id,
        Guid ManagementCompanyId,
        Guid UserId,
        string FullName,
        string Email,
        bool IsActive,
        string TemporaryPassword);
}
