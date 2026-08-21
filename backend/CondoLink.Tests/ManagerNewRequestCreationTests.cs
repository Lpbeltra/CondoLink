using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Requests;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class ManagerNewRequestCreationTests
{
    [Fact]
    public async Task Portal_request_creation_enqueues_manager_template_outbound()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(
            app => app.MapCreateRequest(),
            builder =>
            {
                builder.Services.Configure<WhatsAppOptions>(options =>
                {
                    options.Enabled = true;
                    options.Templates.ManagerNewRequest.Name =
                        "manager_new_request";
                    options.Templates.ManagerNewRequest.Language = "pt_BR";
                });
                builder.Services.AddScoped<WhatsAppNotificationDispatcher>();
            });
        var seeded = await host.WithDbAsync(async db =>
        {
            var condominium = new Condominium(
                "Residencial Monticello", null, null);
            var block = new CondominiumBlock(condominium.Id, "Bloco 1");
            var unit = new Unit(condominium.Id, "1201", block.Id, null, null);
            var category = new Category(condominium.Id, "Garagem", null);
            var resident = CoreTestSeed.User(
                "Tatiana Custódio", "tatiana-portal@example.com");
            var manager = CoreTestSeed.User(
                "Síndico", "sindico-portal@example.com");
            manager.Update("Síndico", "+5511999990002");
            db.AddRange(condominium, block, unit, category, resident, manager);
            CoreTestSeed.AddMember(db, resident.Id, condominium.Id,
                CondominiumRole.Resident);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id,
                CondominiumRole.Manager);
            db.UnitMemberships.Add(new UnitMembership(
                resident.Id, unit.Id, UnitRelationshipType.Owner, true, true));
            await db.SaveChangesAsync();
            return (
                CondominiumId: condominium.Id,
                CategoryId: category.Id,
                UnitId: unit.Id,
                ResidentId: resident.Id);
        });

        var response = await host.ClientFor(seeded.ResidentId).PostAsJsonAsync(
            $"/condominiums/{seeded.CondominiumId}/requests",
            new CreateRequest.RequestDto(seeded.CategoryId, seeded.UnitId,
                "TAG da garagem", "Preciso de uma nova TAG."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await host.WithDbAsync(async db =>
        {
            Assert.Single(await db.Requests.ToArrayAsync());
            var outbound = Assert.Single(await db.WhatsAppOutboundMessages
                .AsNoTracking().ToArrayAsync());
            Assert.Equal(WhatsAppNotificationType.ManagerNewRequest,
                outbound.NotificationType);
            Assert.Equal(WhatsAppSendMode.Template, outbound.SendMode);
            Assert.Equal("manager_new_request", outbound.TemplateName);
            Assert.Equal(
                ["Residencial Monticello", "Tatiana Custódio", "1201", "1",
                    "TAG da garagem"],
                WhatsAppOutboundWorker.ManagerNewRequestTemplateParameters(
                    outbound.TemplateParameterContent));
        });
    }
}
