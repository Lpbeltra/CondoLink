using System.Globalization;
using System.Text;
using System.Text.Json;
using CondoLink.Domain;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class AdministrativeResidentLookupService(
    AppDbContext db,
    IAdministrativeResidentLookupExtractionService extraction,
    ILogger<AdministrativeResidentLookupService> logger,
    AdministrativeUnitResolver unitResolver)
{
    private const string Forbidden =
        "Esse recurso está disponível apenas para a administração do condomínio.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdministrativeWhatsAppResponse?> TryHandleAsync(
        ApplicationUser administrator,
        WhatsAppSession session, string? text, DateTime now, DateTime expires,
        CancellationToken ct)
    {
        var inFlow = session.State is WhatsAppConversationState.CollectingAdminResidentLookup
            or WhatsAppConversationState.SelectingAdminLookupCondominium
            or WhatsAppConversationState.SelectingAdminLookupUnit
            or WhatsAppConversationState.SelectingAdminLookupResident;
        if (!inFlow && !LooksLikeLookup(text)) return null;
        if (inFlow && session.ExpiresAt <= now)
        {
            session.Restart(now, expires);
            return new("Esta consulta expirou. Envie a pergunta novamente.",
                "admin_lookup_expired");
        }
        if (inFlow && (text?.Trim() == "0"
            || string.Equals(text?.Trim(), "cancelar", StringComparison.OrdinalIgnoreCase)))
        {
            session.Restart(now, expires);
            return new("Consulta cancelada.", "admin_lookup_cancelled");
        }

        var draft = ReadDraft(session.DraftAiProposalJson);
        var scope = await AdministrativeScope(administrator.Id, ct);
        if (scope.Count == 0)
        {
            session.Restart(now, expires);
            return new(Forbidden, "admin_lookup_forbidden");
        }

        if (session.State == WhatsAppConversationState.SelectingAdminLookupCondominium
            && int.TryParse(text?.Trim(), out var condominiumChoice))
        {
            if (condominiumChoice < 1 || condominiumChoice > draft.CondominiumChoices.Length)
                return new(CondominiumPrompt(draft.CondominiumChoices),
                    "admin_lookup_condominium_selection_invalid");
            var selected = draft.CondominiumChoices[condominiumChoice - 1];
            draft = draft with { CondominiumId = selected.Id,
                CondominiumName = selected.Name, CondominiumChoices = [] };
            return await ResolveAndRespond(administrator.Id, session, draft, scope,
                now, expires, ct);
        }
        if (session.State == WhatsAppConversationState.SelectingAdminLookupUnit
            && int.TryParse(text?.Trim(), out var unitChoice))
        {
            if (unitChoice < 1 || unitChoice > draft.UnitChoices.Length)
                return new(UnitPrompt(draft.UnitChoices),
                    "admin_lookup_unit_selection_invalid");
            var selected = draft.UnitChoices[unitChoice - 1];
            draft = draft with { UnitId = selected.Id, UnitDisplay = selected.Display,
                UnitChoices = [] };
            return await ResolveAndRespond(administrator.Id, session, draft, scope,
                now, expires, ct);
        }
        if (session.State == WhatsAppConversationState.SelectingAdminLookupResident
            && int.TryParse(text?.Trim(), out var residentChoice))
        {
            if (residentChoice < 1 || residentChoice > draft.ResidentChoices.Length)
                return new(ResidentPrompt(draft.ResidentChoices),
                    "admin_lookup_resident_selection_invalid");
            var selected = draft.ResidentChoices[residentChoice - 1];
            return await RenderSelectedResident(administrator.Id, session, draft,
                selected, scope, now, expires, ct);
        }

        var extracted = await extraction.ExtractAsync(text ?? string.Empty,
            draft.Extraction, ct);
        if (!extracted.Succeeded || extracted.Data is null)
        {
            Save(session, draft, WhatsAppConversationState.CollectingAdminResidentLookup,
                now, expires);
            return new("Não consegui interpretar essa consulta. Tente informar o nome ou a unidade.",
                "admin_lookup_extraction_failed");
        }
        if (extracted.Data.Intent == "unknown") return inFlow
            ? new("Não consegui completar a consulta. Informe o dado solicitado ou envie 0 para cancelar.",
                "admin_lookup_unknown")
            : null;
        draft = draft with { Extraction = Merge(draft.Extraction, extracted.Data) };
        return await ResolveAndRespond(administrator.Id, session, draft, scope,
            now, expires, ct);
    }

    private async Task<AdministrativeWhatsAppResponse> ResolveAndRespond(
        Guid administratorId, WhatsAppSession session, LookupDraft draft,
        IReadOnlyList<ScopedCondominium> scope, DateTime now, DateTime expires,
        CancellationToken ct)
    {
        var data = draft.Extraction!;
        ScopedCondominium? condominium = draft.CondominiumId is Guid selectedId
            ? scope.SingleOrDefault(x => x.Id == selectedId)
            : null;
        if (condominium is null && !string.IsNullOrWhiteSpace(data.Condominium))
        {
            var matches = scope.Where(x => Search(x.Name) == Search(data.Condominium))
                .Take(2).ToArray();
            if (matches.Length == 0)
            {
                session.Restart(now, expires);
                return new(Forbidden, "admin_lookup_outside_scope");
            }
            if (matches.Length == 1) condominium = matches[0];
        }
        if (condominium is null && scope.Count == 1) condominium = scope[0];
        if (condominium is null)
        {
            draft = draft with { CondominiumChoices = scope
                .Select(x => new CondominiumChoice(x.Id, x.Name)).ToArray() };
            Save(session, draft, WhatsAppConversationState.SelectingAdminLookupCondominium,
                now, expires);
            return new(CondominiumPrompt(draft.CondominiumChoices),
                "admin_lookup_condominium_ambiguous");
        }
        draft = draft with { CondominiumId = condominium.Id,
            CondominiumName = condominium.Name, CondominiumChoices = [] };

        if (data.Intent == "unit_residents_lookup" && string.IsNullOrWhiteSpace(data.Unit))
        {
            Save(session, draft, WhatsAppConversationState.CollectingAdminResidentLookup,
                now, expires);
            return new("Qual é a unidade?\n\n0 - Cancelar", "admin_lookup_unit_missing");
        }

        if (!string.IsNullOrWhiteSpace(data.Unit) || draft.UnitId.HasValue)
        {
            var units = (await unitResolver.ResolveAsync(condominium.Id, draft.UnitId,
                data.Unit, data.Block, ct)).Select(x => new UnitChoice(x.Id, x.Display))
                .ToArray();
            if (units.Length == 0)
            {
                session.Restart(now, expires);
                return new("Não encontrei essa unidade. Confira o bloco e o número.",
                    "admin_lookup_unit_not_found");
            }
            if (units.Length > 1)
            {
                draft = draft with { UnitChoices = units };
                Save(session, draft, WhatsAppConversationState.SelectingAdminLookupUnit,
                    now, expires);
                return new(UnitPrompt(units), "admin_lookup_unit_ambiguous");
            }
            draft = draft with { UnitId = units[0].Id, UnitDisplay = units[0].Display,
                UnitChoices = [] };
        }

        if (data.Intent == "unit_residents_lookup")
            return await RenderUnitResidents(administratorId, session, draft,
                scope, now, ct);
        if (string.IsNullOrWhiteSpace(data.ResidentName))
        {
            Save(session, draft, WhatsAppConversationState.CollectingAdminResidentLookup,
                now, expires);
            return new("Qual é o nome do morador?\n\n0 - Cancelar",
                "admin_lookup_resident_name_missing");
        }
        return await ResolveResident(administratorId, session, draft, scope,
            now, expires, ct);
    }

    private async Task<AdministrativeWhatsAppResponse> RenderUnitResidents(
        Guid administratorId, WhatsAppSession session, LookupDraft draft,
        IReadOnlyList<ScopedCondominium> scope, DateTime now, CancellationToken ct)
    {
        if (!Authorized(scope, draft.CondominiumId) || draft.UnitId is null)
            return ForbiddenResponse(session, now);
        var residents = await ActiveResidents(draft.CondominiumId!.Value,
            draft.UnitId, ct);
        session.Restart(now, now.AddMinutes(30));
        logger.LogInformation("Administrative resident lookup completed. Intent: {Intent}; AdministratorUserId: {AdministratorUserId}; CondominiumId: {CondominiumId}; UnitId: {UnitId}; ResultCount: {ResultCount}.",
            "UnitResidentsLookup", administratorId, draft.CondominiumId, draft.UnitId,
            residents.Length);
        if (residents.Length == 0)
            return new($"Não há moradores ativos vinculados à unidade {draft.UnitDisplay}.",
                "admin_lookup_empty");
        var includeEmail = draft.Extraction!.RequestedFields.Contains("email");
        var blocks = residents.Select(x => ResidentBlock(x, includeEmail));
        return new($"Moradores da unidade {draft.UnitDisplay}:\n\n"
            + string.Join("\n\n", blocks), "admin_unit_residents_found");
    }

    private async Task<AdministrativeWhatsAppResponse> ResolveResident(
        Guid administratorId, WhatsAppSession session, LookupDraft draft,
        IReadOnlyList<ScopedCondominium> scope, DateTime now, DateTime expires,
        CancellationToken ct)
    {
        if (!Authorized(scope, draft.CondominiumId))
            return ForbiddenResponse(session, now);
        var residents = await ActiveResidents(draft.CondominiumId!.Value,
            draft.UnitId, ct);
        var sought = Search(draft.Extraction!.ResidentName!);
        var matches = residents.Where(x => Search(x.FullName).Contains(sought,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            session.Restart(now, expires);
            LogLookup(administratorId, draft, 0);
            return new("Não encontrei um morador ativo com esses dados.",
                "admin_resident_not_found");
        }
        if (matches.Length > 1)
        {
            var choices = matches.Select(x => new ResidentChoice(
                x.UserId, x.UnitMembershipId, $"{x.FullName} — {x.UnitDisplay}"))
                .ToArray();
            draft = draft with { ResidentChoices = choices };
            Save(session, draft, WhatsAppConversationState.SelectingAdminLookupResident,
                now, expires);
            return new(ResidentPrompt(choices), "admin_lookup_resident_ambiguous");
        }
        var selected = new ResidentChoice(matches[0].UserId,
            matches[0].UnitMembershipId, matches[0].UnitDisplay);
        return await RenderSelectedResident(administratorId, session, draft,
            selected, scope, now, expires, ct);
    }

    private async Task<AdministrativeWhatsAppResponse> RenderSelectedResident(
        Guid administratorId, WhatsAppSession session, LookupDraft draft,
        ResidentChoice selected, IReadOnlyList<ScopedCondominium> scope,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        if (!Authorized(scope, draft.CondominiumId))
            return ForbiddenResponse(session, now);
        var resident = (await ActiveResidents(draft.CondominiumId!.Value, null, ct))
            .SingleOrDefault(x => x.UserId == selected.UserId
                && x.UnitMembershipId == selected.UnitMembershipId);
        if (resident is null)
        {
            session.Restart(now, expires);
            return new("O vínculo selecionado não está mais ativo.",
                "admin_lookup_resident_stale");
        }
        session.Restart(now, expires);
        LogLookup(administratorId, draft, 1, resident.UnitId);
        var includeEmail = draft.Extraction!.RequestedFields.Contains("email");
        var response = $"{resident.FullName}\n\nUnidade: {resident.UnitDisplay}\n"
            + $"Relação: {RelationshipLabel(resident.Relationship)}\n"
            + $"Telefone: {FormatPhone(resident.Phone)}";
        if (includeEmail) response += $"\nE-mail: {resident.Email}";
        return new(response, "admin_resident_found");
    }

    private Task<ResidentRow[]> ActiveResidents(Guid condominiumId, Guid? unitId,
        CancellationToken ct) =>
        (from link in db.UnitMemberships.AsNoTracking()
         join user in db.Users.AsNoTracking() on link.UserId equals user.Id
         join unit in db.Units.AsNoTracking() on link.UnitId equals unit.Id
         join block in db.CondominiumBlocks.AsNoTracking() on unit.BlockId equals block.Id into blocks
         from block in blocks.DefaultIfEmpty()
         where unit.CondominiumId == condominiumId && unit.IsActive
            && link.IsActive && link.EndedAt == null && link.IsResident && user.IsActive
            && (!unitId.HasValue || unit.Id == unitId)
         orderby user.FullName, user.Id
         select new ResidentRow(user.Id, link.Id, unit.Id, user.FullName,
            user.Email!, user.PhoneNumber, link.RelationshipType,
            block == null ? unit.Identifier : $"Bloco {block.Identifier} - {unit.Identifier}"))
        .ToArrayAsync(ct);

    private async Task<IReadOnlyList<ScopedCondominium>> AdministrativeScope(
        Guid userId, CancellationToken ct)
    {
        var platformAdmin = await db.UserRoles.AsNoTracking().Join(db.Roles.AsNoTracking(),
            link => link.RoleId, role => role.Id,
            (link, role) => new { link.UserId, role.NormalizedName })
            .AnyAsync(x => x.UserId == userId
                && x.NormalizedName == DependencyInjection.PlatformAdminRole.ToUpper(), ct);
        if (platformAdmin)
            return await db.Condominiums.AsNoTracking().Where(x => x.IsActive)
                .OrderBy(x => x.Name).Select(x => new ScopedCondominium(x.Id, x.Name))
                .ToArrayAsync(ct);
        return await (from membership in db.CondominiumMemberships.AsNoTracking()
            join role in db.CondominiumMembershipRoles.AsNoTracking()
                on membership.Id equals role.CondominiumMembershipId
            join condominium in db.Condominiums.AsNoTracking()
                on membership.CondominiumId equals condominium.Id
            where membership.UserId == userId && membership.IsActive
                && membership.EndedAt == null && role.Role == CondominiumRole.Manager
                && role.IsActive && role.RevokedAt == null && condominium.IsActive
            orderby condominium.Name
            select new ScopedCondominium(condominium.Id, condominium.Name))
            .Distinct().ToArrayAsync(ct);
    }

    private void LogLookup(Guid administratorId, LookupDraft draft, int count,
        Guid? unitId = null) => logger.LogInformation(
        "Administrative resident lookup completed. Intent: {Intent}; AdministratorUserId: {AdministratorUserId}; CondominiumId: {CondominiumId}; UnitId: {UnitId}; ResultCount: {ResultCount}.",
        "ResidentLookup", administratorId, draft.CondominiumId, unitId ?? draft.UnitId,
        count);
    private static bool Authorized(IReadOnlyList<ScopedCondominium> scope,
        Guid? condominiumId) => condominiumId.HasValue
            && scope.Any(x => x.Id == condominiumId.Value);
    private static AdministrativeWhatsAppResponse ForbiddenResponse(
        WhatsAppSession session, DateTime now)
    {
        session.End(now);
        return new(Forbidden, "admin_lookup_forbidden");
    }
    private static bool LooksLikeLookup(string? text)
    {
        var value = Search(text ?? string.Empty);
        return value.Contains("morador") || value.Contains("quem mora")
            || value.Contains("telefone") || value.Contains("dados")
            || value.Contains("infos") || value.Contains("informacoes")
            || value.Contains("quem e ") || value.Contains("apto")
            || value.Contains("apartamento") || value.Contains("bloco")
            || value.Contains('/');
    }
    private static string Search(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
    private static string FormatPhone(string? value)
    {
        var normalized = Domain.PhoneNumberNormalizer.Normalize(value);
        if (normalized is null) return "Não informado";
        if (!normalized.StartsWith("+55", StringComparison.Ordinal))
            return normalized;
        var digits = normalized[3..];
        return digits.Length == 11
            ? $"({digits[..2]}) {digits.Substring(2, 5)}-{digits[7..]}"
            : $"({digits[..2]}) {digits.Substring(2, 4)}-{digits[6..]}";
    }
    private static string RelationshipLabel(UnitRelationshipType relationship) => relationship switch
    {
        UnitRelationshipType.Owner => "Proprietário",
        UnitRelationshipType.Tenant => "Inquilino",
        _ => "Morador autorizado"
    };
    private static string ResidentBlock(ResidentRow resident, bool includeEmail)
    {
        var value = $"{resident.FullName}\n{RelationshipLabel(resident.Relationship)}\n"
            + $"Telefone: {FormatPhone(resident.Phone)}";
        return includeEmail ? value + $"\nE-mail: {resident.Email}" : value;
    }
    private static AdministrativeResidentLookupExtraction Merge(
        AdministrativeResidentLookupExtraction? old,
        AdministrativeResidentLookupExtraction next) => new(next.Intent,
            next.ResidentName ?? old?.ResidentName,
            next.Condominium ?? old?.Condominium,
            next.Block ?? old?.Block, next.Unit ?? old?.Unit,
            next.RequestedFields.Length == 0
                ? old?.RequestedFields ?? [] : next.RequestedFields);
    private static void Save(WhatsAppSession session, LookupDraft draft,
        WhatsAppConversationState state, DateTime now, DateTime expires) =>
        session.SetAdministrativeDraft(JsonSerializer.Serialize(draft, JsonOptions),
            state, now, expires, draft.CondominiumId, draft.UnitId);
    private static LookupDraft ReadDraft(string? json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? new()
            : JsonSerializer.Deserialize<LookupDraft>(json, JsonOptions) ?? new(); }
        catch (JsonException) { return new(); }
    }
    private static string CondominiumPrompt(IReadOnlyList<CondominiumChoice> choices) =>
        "Em qual condomínio?\n\n" + string.Join("\n",
            choices.Select((x, i) => $"{i + 1} - {x.Name}")) + "\n0 - Cancelar";
    private static string UnitPrompt(IReadOnlyList<UnitChoice> choices) =>
        "Encontrei mais de uma unidade:\n\n" + string.Join("\n",
            choices.Select((x, i) => $"{i + 1} - {x.Display}")) + "\n0 - Cancelar";
    private static string ResidentPrompt(IReadOnlyList<ResidentChoice> choices) =>
        "Encontrei mais de um morador:\n\n" + string.Join("\n",
            choices.Select((x, i) => $"{i + 1} - {x.Display}")) + "\n0 - Cancelar";

    private sealed record ScopedCondominium(Guid Id, string Name);
    private sealed record CondominiumChoice(Guid Id, string Name);
    private sealed record UnitChoice(Guid Id, string Display);
    private sealed record ResidentChoice(Guid UserId, Guid UnitMembershipId, string Display);
    private sealed record ResidentRow(Guid UserId, Guid UnitMembershipId, Guid UnitId,
        string FullName, string Email, string? Phone, UnitRelationshipType Relationship,
        string UnitDisplay);
    private sealed record LookupDraft(
        AdministrativeResidentLookupExtraction? Extraction = null,
        Guid? CondominiumId = null, string? CondominiumName = null,
        Guid? UnitId = null, string? UnitDisplay = null,
        CondominiumChoice[]? Condominiums = null, UnitChoice[]? Units = null,
        ResidentChoice[]? Residents = null)
    {
        public CondominiumChoice[] CondominiumChoices { get; init; } = Condominiums ?? [];
        public UnitChoice[] UnitChoices { get; init; } = Units ?? [];
        public ResidentChoice[] ResidentChoices { get; init; } = Residents ?? [];
    }
}
