using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class ApplicationUserUpdatePreservationTests
{
    [Fact]
    public async Task Identity_name_email_and_phone_updates_preserve_whatsapp_preference()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        await host.WithServicesAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser("Nome", "preserve@example.com",
                "(11) 99999-0001");
            Assert.True((await manager.CreateAsync(user, "Password123")).Succeeded);

            user.Update("Nome atualizado", "(21) 98888-0002");
            Assert.True((await manager.SetEmailAsync(user,
                "preserved@example.com")).Succeeded);
            Assert.True((await manager.SetUserNameAsync(user,
                "preserved@example.com")).Succeeded);
            Assert.True((await manager.UpdateAsync(user)).Succeeded);

            var reloaded = await manager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(reloaded);
            Assert.True(reloaded!.ReceiveWhatsAppUpdates);
            Assert.Equal("Nome atualizado", reloaded.FullName);
            Assert.Equal("preserved@example.com", reloaded.Email);
            Assert.Equal("+5521988880002", reloaded.NormalizedPhoneNumber);
        });
    }

    [Fact]
    public async Task Explicit_false_survives_unrelated_identity_update()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        await host.WithServicesAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser("Nome", "optout@example.com", null);
            user.SetReceiveWhatsAppUpdates(false);
            Assert.True((await manager.CreateAsync(user, "Password123")).Succeeded);

            user.Update("Outro nome", null);
            Assert.True((await manager.UpdateAsync(user)).Succeeded);

            var reloaded = await manager.FindByIdAsync(user.Id.ToString());
            Assert.False(reloaded!.ReceiveWhatsAppUpdates);
        });
    }
}
