using System.Globalization;
using System.Text;
using System.Text.Json;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class AdministrativeResidentMembershipMutationService(
    AppDbContext db, IAdministrativeResidentMutationExtractionService extraction,
    AdministrativeUnitResolver unitResolver,
    ILogger<AdministrativeResidentMembershipMutationService> logger)
{
    private const string Forbidden =
        "Esse recurso está disponível apenas para a administração do condomínio.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdministrativeWhatsAppResponse?> TryHandleAsync(
        ApplicationUser administrator, WhatsAppSession session, string? text,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        var inFlow = IsMutationState(session.State);
        if (!inFlow && !LooksLikeMutation(text)) return null;
        var scope = await AdministrativeScope(administrator.Id, ct);
        if (scope.Count == 0)
        {
            session.Restart(now, expires);
            return new(Forbidden, "admin_mutation_forbidden");
        }
        if (inFlow && session.ExpiresAt <= now)
        {
            session.Restart(now, expires);
            return new("Esta alteração expirou e foi descartada.", "admin_mutation_expired");
        }
        if (inFlow && text?.Trim() == "0")
        {
            session.Restart(now, expires);
            return new("Alteração cancelada. Nenhum vínculo foi modificado.",
                "admin_mutation_cancelled");
        }
        var draft = ReadDraft(session.DraftAiProposalJson);
        if (session.State == WhatsAppConversationState.ConfirmingAdminResidentMutation)
        {
            if (text?.Trim() == "1")
                return await Confirm(administrator.Id, session, draft, scope, now, expires, ct);
            return new(Confirmation(draft), "admin_mutation_confirmation_invalid");
        }
        if (session.State == WhatsAppConversationState.SelectingAdminMutationCondominium
            && TryChoice(text, draft.CondominiumChoices.Length, out var condominiumIndex))
        {
            var selected = draft.CondominiumChoices[condominiumIndex];
            draft = draft with { CondominiumId = selected.Id,
                CondominiumName = selected.Name, CondominiumChoices = [] };
            return await Resolve(session, administrator.Id, draft, scope, now, expires, ct);
        }
        if (session.State == WhatsAppConversationState.SelectingAdminMutationSourceUnit
            && TryChoice(text, draft.UnitChoices.Length, out var sourceIndex))
        {
            var selected = draft.UnitChoices[sourceIndex];
            draft = draft with { SourceUnitId = selected.Id,
                SourceUnitDisplay = selected.Display, UnitChoices = [] };
            return await Resolve(session, administrator.Id, draft, scope, now, expires, ct);
        }
        if (session.State == WhatsAppConversationState.SelectingAdminMutationDestinationUnit
            && TryChoice(text, draft.UnitChoices.Length, out var destinationIndex))
        {
            var selected = draft.UnitChoices[destinationIndex];
            draft = draft with { DestinationUnitId = selected.Id,
                DestinationUnitDisplay = selected.Display, UnitChoices = [] };
            return await Resolve(session, administrator.Id, draft, scope, now, expires, ct);
        }
        if (session.State == WhatsAppConversationState.SelectingAdminMutationResident
            && TryChoice(text, draft.MembershipChoices.Length, out var residentIndex))
        {
            var selected = draft.MembershipChoices[residentIndex];
            draft = draft with { MembershipId = selected.MembershipId,
                AffectedUserId = selected.UserId, ResidentName = selected.Name,
                SourceUnitId = selected.UnitId, SourceUnitDisplay = selected.UnitDisplay,
                Relationship = selected.Relationship, IsResident = selected.IsResident,
                IsPrimaryResidence = selected.IsPrimaryResidence,
                MembershipChoices = [] };
            return await Resolve(session, administrator.Id, draft, scope, now, expires, ct);
        }

        var result = await extraction.ExtractAsync(text ?? string.Empty,
            draft.Extraction, ct);
        if (!result.Succeeded || result.Data is null || result.Data.Intent == "unknown")
        {
            Save(session, draft, WhatsAppConversationState.CollectingAdminResidentMutation,
                now, expires);
            return new("Não consegui interpretar essa alteração. Informe o morador e a unidade.",
                "admin_mutation_extraction_failed");
        }
        draft = draft with { Extraction = Merge(draft.Extraction, result.Data) };
        return await Resolve(session, administrator.Id, draft, scope, now, expires, ct);
    }

    private async Task<AdministrativeWhatsAppResponse> Resolve(
        WhatsAppSession session, Guid administratorId, MutationDraft draft,
        IReadOnlyList<ScopedCondominium> scope, DateTime now, DateTime expires,
        CancellationToken ct)
    {
        var data = draft.Extraction!;
        var condominium = draft.CondominiumId is Guid selectedId
            ? scope.SingleOrDefault(x => x.Id == selectedId) : null;
        if (condominium is null && !string.IsNullOrWhiteSpace(data.Condominium))
        {
            var matches = scope.Where(x => Search(x.Name) == Search(data.Condominium))
                .ToArray();
            if (matches.Length == 0)
                return ForbiddenResponse(session, now);
            if (matches.Length == 1) condominium = matches[0];
        }
        if (condominium is null && scope.Count == 1) condominium = scope[0];
        if (condominium is null)
        {
            draft = draft with { CondominiumChoices = scope
                .Select(x => new CondominiumChoice(x.Id, x.Name)).ToArray() };
            Save(session, draft, WhatsAppConversationState.SelectingAdminMutationCondominium,
                now, expires);
            return new(CondominiumPrompt(draft.CondominiumChoices),
                "admin_mutation_condominium_ambiguous");
        }
        draft = draft with { CondominiumId = condominium.Id,
            CondominiumName = condominium.Name, CondominiumChoices = [] };

        if (draft.SourceUnitId is null && !string.IsNullOrWhiteSpace(data.SourceUnit))
        {
            var units = await unitResolver.ResolveAsync(condominium.Id, null,
                data.SourceUnit, data.SourceBlock, ct);
            if (units.Length == 0)
                return SafeEnd(session, now,
                    "Não encontrei a unidade de origem. Confira o bloco e o número.",
                    "admin_mutation_source_not_found");
            if (units.Length > 1)
            {
                draft = draft with { UnitChoices = units };
                Save(session, draft, WhatsAppConversationState.SelectingAdminMutationSourceUnit,
                    now, expires);
                return new(UnitPrompt("Encontrei mais de uma unidade:", units),
                    "admin_mutation_source_ambiguous");
            }
            draft = draft with { SourceUnitId = units[0].Id,
                SourceUnitDisplay = units[0].Display };
        }
        if (string.IsNullOrWhiteSpace(data.ResidentName))
        {
            Save(session, draft, WhatsAppConversationState.CollectingAdminResidentMutation,
                now, expires);
            return new("Qual é o nome do morador?\n\n0 - Cancelar",
                "admin_mutation_resident_missing");
        }
        if (draft.MembershipId is null)
        {
            var links = await ActiveMemberships(condominium.Id, draft.SourceUnitId, ct);
            var sought = Search(data.ResidentName);
            var matches = links.Where(x => Search(x.Name).Contains(sought,
                StringComparison.Ordinal)).ToArray();
            if (matches.Length == 0)
                return SafeEnd(session, now,
                    draft.SourceUnitId.HasValue
                        ? $"Não encontrei um vínculo ativo de {data.ResidentName} com a unidade {draft.SourceUnitDisplay}."
                        : "Não encontrei um vínculo residencial ativo para esse morador.",
                    "admin_mutation_membership_not_found");
            if (matches.Length > 1)
            {
                draft = draft with { MembershipChoices = matches };
                Save(session, draft, WhatsAppConversationState.SelectingAdminMutationResident,
                    now, expires);
                return new(MembershipPrompt(data.Intent == "resident_membership_move"
                    && !draft.SourceUnitId.HasValue ? "Qual vínculo deseja alterar?"
                    : "Encontrei mais de um morador:", matches),
                    "admin_mutation_membership_ambiguous");
            }
            var match = matches[0];
            draft = draft with { MembershipId = match.MembershipId,
                AffectedUserId = match.UserId, ResidentName = match.Name,
                SourceUnitId = match.UnitId, SourceUnitDisplay = match.UnitDisplay,
                Relationship = match.Relationship, IsResident = match.IsResident,
                IsPrimaryResidence = match.IsPrimaryResidence };
        }

        if (data.Intent == "resident_membership_move")
        {
            if (draft.DestinationUnitId is null
                && string.IsNullOrWhiteSpace(data.DestinationUnit))
            {
                Save(session, draft, WhatsAppConversationState.CollectingAdminResidentMutation,
                    now, expires);
                return new("Qual é a unidade de destino?\n\n0 - Cancelar",
                    "admin_mutation_destination_missing");
            }
            if (draft.DestinationUnitId is null)
            {
                var units = await unitResolver.ResolveAsync(condominium.Id, null,
                    data.DestinationUnit, data.DestinationBlock, ct);
                if (units.Length == 0)
                    return SafeEnd(session, now,
                        "Não encontrei a unidade de destino. Confira o bloco e o número.",
                        "admin_mutation_destination_not_found");
                if (units.Length > 1)
                {
                    draft = draft with { UnitChoices = units };
                    Save(session, draft,
                        WhatsAppConversationState.SelectingAdminMutationDestinationUnit,
                        now, expires);
                    return new(UnitPrompt("Encontrei mais de uma unidade de destino:", units),
                        "admin_mutation_destination_ambiguous");
                }
                draft = draft with { DestinationUnitId = units[0].Id,
                    DestinationUnitDisplay = units[0].Display };
            }
            if (draft.SourceUnitId == draft.DestinationUnitId)
                return SafeEnd(session, now,
                    "A unidade de destino é a mesma unidade atual.",
                    "admin_mutation_same_unit");
        }
        Save(session, draft, WhatsAppConversationState.ConfirmingAdminResidentMutation,
            now, expires);
        return new(Confirmation(draft), "admin_mutation_confirmation");
    }

    private async Task<AdministrativeWhatsAppResponse> Confirm(
        Guid administratorId, WhatsAppSession session, MutationDraft draft,
        IReadOnlyList<ScopedCondominium> scope, DateTime now, DateTime expires,
        CancellationToken ct)
    {
        if (draft.CondominiumId is null || draft.MembershipId is null
            || draft.AffectedUserId is null || draft.Extraction is null
            || !scope.Any(x => x.Id == draft.CondominiumId))
            return SafeEnd(session, now, "A confirmação não é mais válida.",
                "admin_mutation_stale");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var origin = await (from link in db.UnitMemberships
                join unit in db.Units on link.UnitId equals unit.Id
                where link.Id == draft.MembershipId && link.UserId == draft.AffectedUserId
                    && unit.CondominiumId == draft.CondominiumId
                select link).SingleOrDefaultAsync(ct);
            if (origin is null || !origin.IsActive || origin.EndedAt is not null)
            {
                await transaction.RollbackAsync(ct);
                return SafeEnd(session, now, "O vínculo selecionado não está mais ativo.",
                    "admin_mutation_stale");
            }
            if (draft.Extraction.Intent == "resident_membership_deactivate")
            {
                origin.End(now);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                session.End(now);
                await db.SaveChangesAsync(ct);
                Audit(administratorId, draft, "DeactivateResidentMembership", true);
                return new($"Vínculo encerrado. ✓\n\n{draft.ResidentName} não está mais vinculado à unidade {draft.SourceUnitDisplay}.",
                    "admin_membership_deactivated");
            }
            if (draft.DestinationUnitId is null)
                throw new InvalidOperationException("Destination is missing.");
            var destinationValid = await db.Units.AsNoTracking().AnyAsync(x =>
                x.Id == draft.DestinationUnitId && x.CondominiumId == draft.CondominiumId
                && x.IsActive, ct);
            if (!destinationValid)
            {
                await transaction.RollbackAsync(ct);
                return SafeEnd(session, now, "A unidade de destino não está mais disponível.",
                    "admin_mutation_destination_stale");
            }
            var relationship = origin.RelationshipType;
            if (!string.IsNullOrWhiteSpace(draft.Extraction.Relationship)
                && AdministrativeResidentRegistrationService.TryRelationship(
                    draft.Extraction.Relationship, out var explicitRelationship))
                relationship = explicitRelationship;
            if (await db.UnitMemberships.AnyAsync(x => x.UserId == origin.UserId
                && x.UnitId == draft.DestinationUnitId && x.IsActive, ct))
            {
                await transaction.RollbackAsync(ct);
                return SafeEnd(session, now,
                    "O morador já possui vínculo ativo com a unidade de destino.",
                    "admin_mutation_destination_duplicate");
            }
            var destination = await db.UnitMemberships.SingleOrDefaultAsync(x =>
                x.UserId == origin.UserId && x.UnitId == draft.DestinationUnitId
                && x.RelationshipType == relationship, ct);
            origin.End(now);
            if (destination is null)
                db.UnitMemberships.Add(new UnitMembership(origin.UserId,
                    draft.DestinationUnitId.Value, relationship,
                    origin.IsResident, origin.IsPrimaryResidence));
            else
                destination.Reactivate(origin.IsResident,
                    origin.IsPrimaryResidence, now);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            session.End(now);
            await db.SaveChangesAsync(ct);
            Audit(administratorId, draft, "MoveResidentMembership", true);
            return new($"Unidade alterada. ✓\n\n{draft.ResidentName} foi transferido de {draft.SourceUnitDisplay} para {draft.DestinationUnitDisplay}.",
                "admin_membership_moved");
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(ct);
            logger.LogWarning("Administrative membership mutation failed. AdministratorUserId: {AdministratorUserId}; Action: {Action}; AffectedUserId: {AffectedUserId}; CondominiumId: {CondominiumId}; SourceUnitId: {SourceUnitId}; DestinationUnitId: {DestinationUnitId}; FailureType: {FailureType}.",
                administratorId, draft.Extraction.Intent, draft.AffectedUserId,
                draft.CondominiumId, draft.SourceUnitId, draft.DestinationUnitId,
                exception.GetType().Name);
            throw;
        }
    }

    private void Audit(Guid administratorId, MutationDraft draft, string action,
        bool succeeded) => logger.LogInformation(
        "Administrative membership mutation completed. AdministratorUserId: {AdministratorUserId}; Action: {Action}; AffectedUserId: {AffectedUserId}; CondominiumId: {CondominiumId}; SourceUnitId: {SourceUnitId}; DestinationUnitId: {DestinationUnitId}; Succeeded: {Succeeded}.",
        administratorId, action, draft.AffectedUserId, draft.CondominiumId,
        draft.SourceUnitId, draft.DestinationUnitId, succeeded);

    private async Task<MembershipChoice[]> ActiveMemberships(Guid condominiumId,
        Guid? unitId, CancellationToken ct)
    {
        var rows = await (from link in db.UnitMemberships.AsNoTracking()
         join user in db.Users.AsNoTracking() on link.UserId equals user.Id
         join unit in db.Units.AsNoTracking() on link.UnitId equals unit.Id
         join block in db.CondominiumBlocks.AsNoTracking()
            on unit.BlockId equals block.Id into blocks
         from block in blocks.DefaultIfEmpty()
         where unit.CondominiumId == condominiumId && unit.IsActive && user.IsActive
            && link.IsActive && link.EndedAt == null && link.IsResident
            && (!unitId.HasValue || unit.Id == unitId)
         orderby user.FullName, unit.Identifier
         select new MembershipRow(link.Id, user.Id, user.FullName, unit.Id,
            unit.Identifier, block == null ? null : block.Identifier,
            link.RelationshipType, link.IsResident, link.IsPrimaryResidence))
        .ToArrayAsync(ct);
        return rows.Select(x => new MembershipChoice(x.MembershipId, x.UserId,
            x.Name, x.UnitId, x.Block is null ? x.Unit
                : $"Bloco {DisplayBlock(x.Block)} - {x.Unit}", x.Relationship,
            x.IsResident, x.IsPrimaryResidence)).ToArray();
    }

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
            select new ScopedCondominium(condominium.Id, condominium.Name))
            .Distinct().ToArrayAsync(ct);
    }

    private static bool IsMutationState(WhatsAppConversationState state) => state is
        WhatsAppConversationState.CollectingAdminResidentMutation
        or WhatsAppConversationState.SelectingAdminMutationCondominium
        or WhatsAppConversationState.SelectingAdminMutationResident
        or WhatsAppConversationState.SelectingAdminMutationSourceUnit
        or WhatsAppConversationState.SelectingAdminMutationDestinationUnit
        or WhatsAppConversationState.ConfirmingAdminResidentMutation;
    private static bool LooksLikeMutation(string? text)
    {
        var value = Search(text ?? string.Empty);
        return value.Contains("inativ") || value.Contains("remov")
            || value.Contains("retir") || value.Contains("tira ")
            || value.Contains("nao mora mais")
            || value.Contains("mude") || value.Contains("muda ")
            || value.Contains("alter") || value.Contains("transfir");
    }
    private static bool TryChoice(string? text, int count, out int index)
    {
        index = -1;
        return int.TryParse(text?.Trim(), out var choice) && choice >= 1
            && choice <= count && (index = choice - 1) >= 0;
    }
    private static string Search(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark) builder.Append(character);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
    private static string DisplayBlock(string value) =>
        value.StartsWith("Bloco ", StringComparison.OrdinalIgnoreCase)
            ? value[6..].Trim() : value.Trim();
    private static AdministrativeResidentMutationExtraction Merge(
        AdministrativeResidentMutationExtraction? old,
        AdministrativeResidentMutationExtraction next) => new(next.Intent,
            next.ResidentName ?? old?.ResidentName,
            next.Condominium ?? old?.Condominium,
            next.SourceBlock ?? old?.SourceBlock, next.SourceUnit ?? old?.SourceUnit,
            next.DestinationBlock ?? old?.DestinationBlock,
            next.DestinationUnit ?? old?.DestinationUnit,
            next.Relationship ?? old?.Relationship);
    private static void Save(WhatsAppSession session, MutationDraft draft,
        WhatsAppConversationState state, DateTime now, DateTime expires) =>
        session.SetAdministrativeDraft(JsonSerializer.Serialize(draft, JsonOptions),
            state, now, expires, draft.CondominiumId,
            draft.DestinationUnitId ?? draft.SourceUnitId);
    private static MutationDraft ReadDraft(string? json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? new()
            : JsonSerializer.Deserialize<MutationDraft>(json, JsonOptions) ?? new(); }
        catch (JsonException) { return new(); }
    }
    private static AdministrativeWhatsAppResponse ForbiddenResponse(
        WhatsAppSession session, DateTime now)
    { session.End(now); return new(Forbidden, "admin_mutation_forbidden"); }
    private static AdministrativeWhatsAppResponse SafeEnd(WhatsAppSession session,
        DateTime now, string text, string result)
    { session.End(now); return new(text, result); }
    private static string CondominiumPrompt(IReadOnlyList<CondominiumChoice> choices) =>
        "Em qual condomínio?\n\n" + string.Join("\n",
            choices.Select((x, i) => $"{i + 1} - {x.Name}")) + "\n0 - Cancelar";
    private static string UnitPrompt(string title,
        IReadOnlyList<AdministrativeUnitChoice> choices) => title + "\n\n"
        + string.Join("\n", choices.Select((x, i) => $"{i + 1} - {x.Display}"))
        + "\n0 - Cancelar";
    private static string MembershipPrompt(string title,
        IReadOnlyList<MembershipChoice> choices) => title + "\n\n"
        + string.Join("\n", choices.Select((x, i) =>
            $"{i + 1} - {x.Name} — {x.UnitDisplay}")) + "\n0 - Cancelar";
    private static string Confirmation(MutationDraft draft)
    {
        var relationship = RelationshipLabel(draft.Extraction?.Relationship,
            draft.Relationship);
        return draft.Extraction?.Intent == "resident_membership_move"
            ? $"Confirme a alteração:\n\n{draft.ResidentName}\n\nDe:\n{draft.SourceUnitDisplay}\n\nPara:\n{draft.DestinationUnitDisplay}\n\nRelação: {relationship}\n\n1 - Confirmar\n0 - Cancelar"
            : $"Confirme a alteração:\n\n{draft.ResidentName}\nUnidade atual: {draft.SourceUnitDisplay}\nRelação: {relationship}\n\nO vínculo deste morador com a unidade será encerrado.\n\n1 - Confirmar\n0 - Cancelar";
    }
    private static string RelationshipLabel(string? explicitValue,
        UnitRelationshipType current)
    {
        var value = current;
        if (!string.IsNullOrWhiteSpace(explicitValue)
            && AdministrativeResidentRegistrationService.TryRelationship(
                explicitValue, out var parsed)) value = parsed;
        return value switch { UnitRelationshipType.Owner => "Proprietário",
            UnitRelationshipType.Tenant => "Inquilino", _ => "Morador autorizado" };
    }

    private sealed record ScopedCondominium(Guid Id, string Name);
    private sealed record CondominiumChoice(Guid Id, string Name);
    private sealed record MembershipChoice(Guid MembershipId, Guid UserId,
        string Name, Guid UnitId, string UnitDisplay,
        UnitRelationshipType Relationship, bool IsResident,
        bool IsPrimaryResidence);
    private sealed record MembershipRow(Guid MembershipId, Guid UserId,
        string Name, Guid UnitId, string Unit, string? Block,
        UnitRelationshipType Relationship, bool IsResident,
        bool IsPrimaryResidence);
    private sealed record MutationDraft(
        AdministrativeResidentMutationExtraction? Extraction = null,
        Guid? CondominiumId = null, string? CondominiumName = null,
        Guid? MembershipId = null, Guid? AffectedUserId = null,
        string? ResidentName = null, Guid? SourceUnitId = null,
        string? SourceUnitDisplay = null, Guid? DestinationUnitId = null,
        string? DestinationUnitDisplay = null,
        UnitRelationshipType Relationship = UnitRelationshipType.AuthorizedOccupant,
        bool IsResident = true, bool IsPrimaryResidence = false,
        CondominiumChoice[]? Condominiums = null,
        AdministrativeUnitChoice[]? Units = null,
        MembershipChoice[]? Memberships = null)
    {
        public CondominiumChoice[] CondominiumChoices { get; init; } = Condominiums ?? [];
        public AdministrativeUnitChoice[] UnitChoices { get; init; } = Units ?? [];
        public MembershipChoice[] MembershipChoices { get; init; } = Memberships ?? [];
    }
}
