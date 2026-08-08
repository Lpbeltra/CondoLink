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
        var password = app.Configuration["PlatformAdmin:Password"];

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
            var activePlatformAdminExists = (await userManager.GetUsersInRoleAsync(
                    DependencyInjection.PlatformAdminRole))
                .Any(existing => existing.IsActive
                    && !string.IsNullOrWhiteSpace(existing.NormalizedEmail));
            if (activePlatformAdminExists)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "PlatformAdmin:Password is required to create the platform admin user.");
            }

            user = new ApplicationUser(
                "Platform Administrator",
                email,
                phoneNumber: null);

            var createResult = await userManager.CreateAsync(
                user,
                password);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(
                        " ",
                        createResult.Errors.Select(
                            error => error.Description)));
            }
        }

        if (!user.IsActive)
        {
            user.SetActiveStatus(true);

            var updateResult = await userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(
                        " ",
                        updateResult.Errors.Select(
                            error => error.Description)));
            }
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
