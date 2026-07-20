using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CondoLink.Api;

public static class PlatformAdminInitializer
{
    public static async Task InitializePlatformAdminAsync(
        this WebApplication app)
    {
        var email = app.Configuration["PlatformAdmin:Email"];

        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        await using var scope =
            app.Services.CreateAsyncScope();

        var roleManager =
            scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

        var roleExists =
            await roleManager.RoleExistsAsync(
                DependencyInjection.PlatformAdminRole);

        if (!roleExists)
        {
            var roleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>(
                    DependencyInjection.PlatformAdminRole));

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(
                        " ",
                        roleResult.Errors.Select(
                            error => error.Description)));
            }
        }

        var user = await userManager.FindByEmailAsync(
            email.Trim());

        if (user is null)
        {
            throw new InvalidOperationException(
                $"Platform admin user '{email}' was not found.");
        }

        if (!await userManager.IsInRoleAsync(
                user,
                DependencyInjection.PlatformAdminRole))
        {
            var result = await userManager.AddToRoleAsync(
                user,
                DependencyInjection.PlatformAdminRole);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(
                        " ",
                        result.Errors.Select(
                            error => error.Description)));
            }
        }
    }
}