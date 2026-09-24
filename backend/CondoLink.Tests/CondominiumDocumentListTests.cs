using System.Net;
using System.Text.Json;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class CondominiumDocumentListTests
{
    [Fact]
    public async Task List_returns_semantic_document_types_for_the_frontend()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(
            app => app.MapCondominiumAssistant(),
            builder =>
            {
                builder.Services.AddSingleton<IEmbeddingService, LocalEmbeddingService>();
                builder.Services.AddSingleton<LocalFileStorage>();
                builder.Services.AddSingleton<ICondominiumDocumentStorage>(services => services.GetRequiredService<LocalFileStorage>());
                builder.Services.AddScoped<CondominiumDocumentProcessor>();
                builder.Services.AddScoped<CondominiumAssistantService>();
            });

        var (condominiumId, managerId) = await host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Aurora", null, null);
            var manager = CoreTestSeed.User("Síndico", "manager@aurora.test");
            db.AddRange(condominium, manager);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            db.Add(new CondominiumDocument(condominium.Id, "Regimento Interno", CondominiumDocumentType.InternalRules,
                "regimento.pdf", "regimento.pdf", "application/pdf", 1, null, manager.Id));
            db.Add(new CondominiumDocument(condominium.Id, "Manual de Mudanças", CondominiumDocumentType.Manual,
                "manual.pdf", "manual.pdf", "application/pdf", 1, null, manager.Id));
            await db.SaveChangesAsync();
            return (condominium.Id, manager.Id);
        });

        var response = await host.ClientFor(managerId).GetAsync($"/condominiums/{condominiumId}/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var documents = body.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, documents.Length);
        Assert.Contains(documents, x => x.GetProperty("name").GetString() == "Regimento Interno"
            && x.GetProperty("documentType").GetString() == "InternalRules");
        Assert.Contains(documents, x => x.GetProperty("name").GetString() == "Manual de Mudanças"
            && x.GetProperty("documentType").GetString() == "Manual");
    }
}
