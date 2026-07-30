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
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.JobTitle))
        {
            return Results.BadRequest(new
            {
                message = "Full name, email and job title are required."
            });
        }

        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var contact = Domain.RegistrationData.Optional(request.Contact);
        var jobTitle = request.JobTitle.Trim();
        if (fullName.Length > 200 || email.Length > 254 ||
            contact?.Length > 30 || jobTitle.Length > 100)
        {
            return Results.BadRequest(new
            {
                message = "Employee data exceeds the allowed length."
            });
        }
        var normalizedContact =
            Domain.BrazilianPhoneNumber.Normalize(contact);
        if (contact is not null && normalizedContact is null)
        {
            return Results.BadRequest(new
                { message = "Contact must be a valid Brazilian phone number." });
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
        if (normalizedContact is not null
            && await dbContext.Users.AsNoTracking().AnyAsync(
                user => user.NormalizedPhoneNumber == normalizedContact,
                cancellationToken))
        {
            return Results.Conflict(new
                { message = "A user with this phone number already exists." });
        }

        var temporaryPassword = GenerateTemporaryPassword();
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = new ApplicationUser(fullName, email, contact);
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

        var employee = new ManagementCompanyEmployee(
            managementCompanyId,
            user.Id,
            jobTitle);
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
                user.PhoneNumber,
                employee.JobTitle,
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

    public sealed record Request(
        string? FullName, string? Email, string? Contact, string? JobTitle);

    public sealed record CreatedResponse(
        Guid Id,
        Guid ManagementCompanyId,
        Guid UserId,
        string FullName,
        string Email,
        string? Contact,
        string JobTitle,
        bool IsActive,
        string TemporaryPassword);
}
