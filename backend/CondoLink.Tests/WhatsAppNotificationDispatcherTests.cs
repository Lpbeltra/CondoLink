using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class WhatsAppNotificationDispatcherTests
{
    [Fact]
    public async Task Enabled_user_condominium_and_membership_enqueue_message()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var requestId = await SeedAsync(host, UserCondition.Enabled);

        await DispatchAsync(host, requestId);

        var outbound = await host.WithDbAsync(db =>
            db.WhatsAppOutboundMessages.AsNoTracking().SingleAsync());
        Assert.Equal(WhatsAppOutboundStatus.Pending, outbound.Status);
        Assert.Null(outbound.LastErrorDescription);
    }

    [Theory]
    [InlineData(UserCondition.PreferenceDisabled, "Preferência desabilitada.")]
    [InlineData(UserCondition.MissingPhone, "Telefone inválido.")]
    [InlineData(UserCondition.Inactive, "Usuário inativo.")]
    [InlineData(UserCondition.MissingMembership, "Vínculo inativo.")]
    public async Task User_eligibility_failures_remain_blocked(
        UserCondition condition, string reason)
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        var requestId = await SeedAsync(host, condition);

        await DispatchAsync(host, requestId);

        var outbound = await host.WithDbAsync(db =>
            db.WhatsAppOutboundMessages.AsNoTracking().SingleAsync());
        Assert.Equal(WhatsAppOutboundStatus.Skipped, outbound.Status);
        Assert.Equal(reason, outbound.LastErrorDescription);
    }

    private static Task DispatchAsync(CoreEndpointTestHost host, Guid requestId) =>
        host.WithDbAsync(async db =>
        {
            var dispatcher = new WhatsAppNotificationDispatcher(db,
                Options.Create(new WhatsAppOptions { Enabled = true }),
                NullLogger<WhatsAppNotificationDispatcher>.Instance);
            await dispatcher.EnqueueAsync(requestId,
                WhatsAppNotificationType.StatusChanged,
                $"dispatcher-test:{Guid.NewGuid():N}", "status", null,
                CancellationToken.None);
        });

    private static Task<Guid> SeedAsync(CoreEndpointTestHost host,
        UserCondition condition) => host.WithDbAsync(async db =>
    {
        var condominium = new Condominium("Residencial", null, null);
        var category = new Category(condominium.Id, "Outros", null);
        var phone = condition == UserCondition.MissingPhone
            ? null : "(11) 99999-0001";
        var user = new ApplicationUser("Morador", $"{Guid.NewGuid():N}@example.com", phone);
        user.NormalizedUserName = user.Email!.ToUpperInvariant();
        user.NormalizedEmail = user.NormalizedUserName;
        if (condition == UserCondition.PreferenceDisabled)
            user.SetReceiveWhatsAppUpdates(false);
        if (condition == UserCondition.Inactive) user.SetActiveStatus(false);
        var request = new CondoLink.Domain.Entities.Request(condominium.Id,
            user.Id, null, category.Id, "Solicitação", "Descrição");
        db.AddRange(condominium, category, user, request);
        if (condition == UserCondition.Enabled)
            db.Add(new WhatsAppInboundMessage($"wamid.{Guid.NewGuid():N}",
                user.NormalizedPhoneNumber!, "text", "menu", DateTime.UtcNow));
        if (condition != UserCondition.MissingMembership)
            db.Add(new CondominiumMembership(user.Id, condominium.Id));
        await db.SaveChangesAsync();
        return request.Id;
    });

    public enum UserCondition
    {
        Enabled,
        PreferenceDisabled,
        MissingPhone,
        Inactive,
        MissingMembership
    }
}
