using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Features.OperationalMessages;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

namespace CondoLink.Api.Features.Overwatch.OperationalMessages;

public sealed record UpdateOperationalMessageRequest(string Prefix, string Suffix);

public static class OperationalMessageEndpoints
{
    public static IEndpointRouteBuilder MapOperationalMessageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/messages", ListAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        endpoints.MapPut("/overwatch/messages/{key}", UpdateAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        endpoints.MapDelete("/overwatch/messages/{key}", RestoreAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        return endpoints;
    }

    private static async Task<IResult> ListAsync([FromServices] OperationalMessageTemplateService service,
        AppDbContext db, IOptions<WhatsAppOptions> options, CancellationToken ct)
    {
        var overrides = await db.OperationalMessageTemplates.AsNoTracking().ToDictionaryAsync(x => x.Key, ct);
        var result = OperationalMessageTemplateService.Definitions.Select(definition =>
        {
            overrides.TryGetValue(definition.Key, out var configured);
            var meta = MetaFor(definition, options.Value.Templates);
            return Response(definition, configured?.Prefix ?? definition.Prefix,
                configured?.Suffix ?? definition.Suffix, configured, meta.Name, meta.Language);
        });
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateAsync(string key, UpdateOperationalMessageRequest request,
        ClaimsPrincipal principal, AppDbContext db, IOptions<WhatsAppOptions> options, CancellationToken ct)
    {
        var definition = OperationalMessageTemplateService.Definition(key);
        if (definition is null) return Results.NotFound(new { message = "Gatilho não encontrado." });
        var prefix = request.Prefix ?? "";
        var suffix = request.Suffix ?? "";
        var error = OperationalMessageTemplateService.Validate(prefix, suffix);
        if (error is not null) return Results.BadRequest(new { message = error });
        if (!UserId(principal, out var userId)) return Results.Unauthorized();
        var entity = await db.OperationalMessageTemplates.SingleOrDefaultAsync(x => x.Key == key, ct);
        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new OperationalMessageTemplate(key, prefix, suffix, userId, now);
            db.OperationalMessageTemplates.Add(entity);
        }
        else entity.Update(prefix, suffix, userId, now);
        await db.SaveChangesAsync(ct);
        var meta = MetaFor(definition, options.Value.Templates);
        return Results.Ok(Response(definition, entity.Prefix, entity.Suffix, entity, meta.Name, meta.Language));
    }

    private static async Task<IResult> RestoreAsync(string key, AppDbContext db,
        IOptions<WhatsAppOptions> options, CancellationToken ct)
    {
        var definition = OperationalMessageTemplateService.Definition(key);
        if (definition is null) return Results.NotFound(new { message = "Gatilho não encontrado." });
        var entity = await db.OperationalMessageTemplates.SingleOrDefaultAsync(x => x.Key == key, ct);
        if (entity is not null) { db.Remove(entity); await db.SaveChangesAsync(ct); }
        var meta = MetaFor(definition, options.Value.Templates);
        return Results.Ok(Response(definition, definition.Prefix, definition.Suffix, null, meta.Name, meta.Language));
    }

    private static object Response(OperationalMessageDefinition definition, string prefix,
        string suffix, OperationalMessageTemplate? configured, string? metaName, string? language) => new
        {
            definition.Key, definition.Title, definition.Description, prefix, suffix,
            structuralSuffix = definition.StructuralSuffix,
            dynamicContent = "{MensagemDoSindico}",
            mode = "SessionAndMetaFallback", modeLabel = "Sessão + fallback Meta",
            metaTemplateName = metaName, metaTemplateLanguage = language,
            metaQuickReplies = definition.Key switch
            {
                "WaitingForResidentClosure" => new[] { "Finalizar atendimento", "Ainda tenho uma dúvida" },
                "WaitingForResident" => new[] { "Responder agora", "Lembrar-me em 3 horas" },
                "Resolved" => Array.Empty<string>(),
                _ => new[] { "Ver atualização" }
            },
            isOverride = configured is not null, configured?.UpdatedAt, configured?.UpdatedByUserId,
            partMaximumLength = OperationalMessageTemplateService.PartMaximumLength,
            outboundMaximumLength = OperationalMessageTemplateService.OutboundMaximumLength
        };

    private static WhatsAppTemplateDefinition MetaFor(OperationalMessageDefinition definition,
        WhatsAppTemplateOptions options)
    {
        var configured = definition.Key switch
        {
            "WaitingForResident" => options.InformationRequested,
            "WaitingForResidentClosure" => options.ResidentClosureConfirmation,
            "Resolved" => options.Resolved,
            _ => options.StatusChanged
        };
        if (string.IsNullOrWhiteSpace(configured.Name)) return new WhatsAppTemplateDefinition
        { Name = definition.Key switch
            {
                "WaitingForResident" or "WaitingForResidentClosure" or "Resolved" => null,
                _ => "request_status_update"
            }, Language = "pt_BR" };
        return configured;
    }

    private static bool UserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out userId);
    }
}
