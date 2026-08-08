using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using CondoLink.Domain;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.WhatsApp;

public sealed record AdministrativeWhatsAppResponse(string Text, string Result);

public sealed class AdministrativeResidentRegistrationService(
    AppDbContext db, UserManager<ApplicationUser> userManager,
    IAdministrativeResidentExtractionService extraction,
    ILogger<AdministrativeResidentRegistrationService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdministrativeWhatsAppResponse?> TryHandleAsync(
        ApplicationUser administrator, WhatsAppSession session, string? text,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        var inFlow = session.State is WhatsAppConversationState.CollectingAdminResidentData
            or WhatsAppConversationState.SelectingAdminResidentUnit
            or WhatsAppConversationState.ConfirmingAdminResident
            or WhatsAppConversationState.CorrectingAdminResident;
        var command = IsCommand(text);
        if (!inFlow && !command) return null;

        var scope = await AdministrativeScope(administrator.Id, ct);
        if (scope.Count == 0)
        {
            session.Restart(now, expires);
            return new("Este comando está disponível somente para administradores autorizados.",
                "admin_command_forbidden");
        }
        if (session.ExpiresAt <= now && inFlow)
        {
            session.Restart(now, expires);
            return new("Este cadastro expirou e foi descartado. Envie “Cadastrar morador” para começar novamente.",
                "admin_registration_expired");
        }
        if (text?.Trim() is "0" || string.Equals(text?.Trim(), "cancelar", StringComparison.OrdinalIgnoreCase))
        {
            session.Restart(now, expires);
            return new("Cadastro cancelado. Nenhuma alteração foi realizada.", "admin_registration_cancelled");
        }

        var draft = ReadDraft(session.DraftAiProposalJson);
        if (session.State == WhatsAppConversationState.SelectingAdminResidentUnit
            && int.TryParse(text?.Trim(), out var choice))
        {
            if (choice < 1 || choice > draft.UnitChoices.Length)
                return new(UnitChoicesPrompt(draft.UnitChoices), "admin_unit_selection_invalid");
            var selected = draft.UnitChoices[choice - 1];
            draft = draft with { UnitId = selected.Id, UnitDisplay = selected.Display, UnitChoices = [] };
            return await ValidateAndPrompt(administrator, session, draft, scope, now, expires, ct);
        }
        if (session.State == WhatsAppConversationState.ConfirmingAdminResident)
        {
            if (text?.Trim() == "1")
                return await Confirm(administrator, session, draft, scope, now, expires, ct);
            if (text?.Trim() == "2")
            {
                session.SetAdministrativeDraft(JsonSerializer.Serialize(draft, JsonOptions),
                    WhatsAppConversationState.CorrectingAdminResident, now, expires,
                    draft.CondominiumId, draft.UnitId);
                return new("Envie somente a correção necessária.", "admin_registration_correcting");
            }
            return new(Confirmation(draft), "admin_confirmation_invalid");
        }

        var ai = await extraction.ExtractAsync(text ?? string.Empty, draft.Extraction, ct);
        if (!ai.Succeeded || ai.Data is null)
        {
            session.Restart(now, expires);
            return new("Não consegui interpretar os dados do cadastro. Nenhuma alteração foi realizada. Tente novamente mais tarde.",
                "admin_extraction_failed");
        }
        if (!command && !inFlow && ai.Data.Intent != "register_resident") return null;
        draft = draft with { Extraction = Merge(draft.Extraction, ai.Data) };
        return await ValidateAndPrompt(administrator, session, draft, scope, now, expires, ct);
    }

    private async Task<AdministrativeWhatsAppResponse> ValidateAndPrompt(
        ApplicationUser administrator, WhatsAppSession session, AdminDraft draft,
        IReadOnlyList<ScopedCondominium> scope, DateTime now, DateTime expires, CancellationToken ct)
    {
        var data = draft.Extraction;
        if (data is null) return Missing(session, draft, "Envie os dados do morador.", now, expires);
        if (string.IsNullOrWhiteSpace(data.FullName)) return Missing(session, draft, "Qual é o nome completo do morador?", now, expires);
        if (data.FullName.Trim().Length > 200) return Missing(session, draft, "O nome deve ter no máximo 200 caracteres. Envie o nome correto.", now, expires);
        if (string.IsNullOrWhiteSpace(data.Email)) return Missing(session, draft, "Qual é o e-mail do morador?", now, expires);
        var email = data.Email.Trim().ToLowerInvariant();
        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
            return Missing(session, draft, "O e-mail informado não é válido. Qual é o e-mail correto?", now, expires);
        if (string.IsNullOrWhiteSpace(data.Phone))
            return Missing(session, draft, "Qual é o telefone do morador?", now, expires);
        if (BrazilianPhoneNumber.Normalize(data.Phone) is null)
            return Missing(session, draft, "O telefone informado não é um número brasileiro válido. Qual é o telefone correto?", now, expires);
        if (string.IsNullOrWhiteSpace(data.Relationship))
            return Missing(session, draft, "Qual é a relação com a unidade?\n\n1 - Proprietário\n2 - Inquilino\n3 - Ocupante autorizado", now, expires);
        if (!TryRelationship(data.Relationship, out _))
            return Missing(session, draft, "Relação inválida. Escolha: Proprietário, Inquilino ou Ocupante autorizado.", now, expires);

        ScopedCondominium? condominium = null;
        if (!string.IsNullOrWhiteSpace(data.Condominium))
        {
            var matches = scope.Where(x => Equivalent(x.Name, data.Condominium)).Take(2).ToArray();
            if (matches.Length == 1) condominium = matches[0];
            else if (matches.Length == 0)
                return Missing(session, draft, "Não encontrei esse condomínio dentro do seu acesso administrativo. Confira o nome.", now, expires);
        }
        else if (scope.Count == 1) condominium = scope[0];
        if (condominium is null)
            return Missing(session, draft, "Em qual condomínio o morador será cadastrado?", now, expires);
        if (string.IsNullOrWhiteSpace(data.Unit))
            return Missing(session, draft with { CondominiumId = condominium.Id }, "Qual é a unidade do morador?", now, expires);

        var units = draft.UnitId is Guid selectedUnitId
            ? await (from unit in db.Units.AsNoTracking()
                join block in db.CondominiumBlocks.AsNoTracking() on unit.BlockId equals block.Id into blocks
                from block in blocks.DefaultIfEmpty()
                where unit.Id == selectedUnitId && unit.CondominiumId == condominium.Id && unit.IsActive
                select new UnitChoice(unit.Id,
                    block == null ? unit.Identifier : $"Bloco {block.Identifier} - {unit.Identifier}"))
                .ToArrayAsync(ct)
            : await (from unit in db.Units.AsNoTracking()
            join block in db.CondominiumBlocks.AsNoTracking() on unit.BlockId equals block.Id into blocks
            from block in blocks.DefaultIfEmpty()
            where unit.CondominiumId == condominium.Id && unit.IsActive
                && unit.Identifier.ToLower() == data.Unit.Trim().ToLower()
                && (string.IsNullOrWhiteSpace(data.Block)
                    || (block != null && block.Identifier.ToLower() == data.Block.Trim().ToLower()))
            orderby block == null ? "" : block.Identifier, unit.Identifier
            select new UnitChoice(unit.Id,
                block == null ? unit.Identifier : $"Bloco {block.Identifier} - {unit.Identifier}"))
            .Take(10).ToArrayAsync(ct);
        if (units.Length == 0)
            return Missing(session, draft with { CondominiumId = condominium.Id },
                "Não encontrei essa unidade no condomínio. Confira o bloco e o número ou cadastre a unidade pelo portal.", now, expires);
        if (units.Length > 1)
        {
            draft = draft with { CondominiumId = condominium.Id, CondominiumName = condominium.Name,
                UnitChoices = units };
            session.SetAdministrativeDraft(JsonSerializer.Serialize(draft, JsonOptions),
                WhatsAppConversationState.SelectingAdminResidentUnit, now, expires, condominium.Id);
            return new(UnitChoicesPrompt(units), "admin_unit_ambiguous");
        }
        draft = draft with { CondominiumId = condominium.Id, CondominiumName = condominium.Name,
            UnitId = units[0].Id, UnitDisplay = units[0].Display, UnitChoices = [] };

        var normalizedPhone = BrazilianPhoneNumber.Normalize(data.Phone);
        var existing = await db.Users.AsNoTracking().Where(x =>
                x.NormalizedEmail == email.ToUpper() ||
                (normalizedPhone != null && x.NormalizedPhoneNumber == normalizedPhone))
            .Take(2).ToArrayAsync(ct);
        if (existing.Length > 1)
            return Missing(session, draft, "E-mail e telefone correspondem a cadastros diferentes. Corrija os dados pelo portal.", now, expires);
        if (existing is [var user] && !user.IsActive)
            return Missing(session, draft, "O usuário encontrado está inativo e não pode ser vinculado.", now, expires);
        draft = draft with { ExistingUserId = existing.SingleOrDefault()?.Id,
            ExistingUserName = existing.SingleOrDefault()?.FullName };
        if (draft.ExistingUserId is Guid userId && await db.UnitMemberships.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.UnitId == draft.UnitId && x.IsActive, ct))
        {
            session.Restart(now, expires);
            return new($"{draft.ExistingUserName} já está vinculado à unidade {draft.UnitDisplay}.",
                "admin_unit_membership_duplicate");
        }
        session.SetAdministrativeDraft(JsonSerializer.Serialize(draft, JsonOptions),
            WhatsAppConversationState.ConfirmingAdminResident, now, expires,
            draft.CondominiumId, draft.UnitId);
        return new(Confirmation(draft), "admin_registration_confirmation");
    }

    private async Task<AdministrativeWhatsAppResponse> Confirm(
        ApplicationUser administrator, WhatsAppSession session, AdminDraft draft,
        IReadOnlyList<ScopedCondominium> scope, DateTime now, DateTime expires, CancellationToken ct)
    {
        if (draft.CondominiumId is null || draft.UnitId is null || draft.Extraction is null
            || !scope.Any(x => x.Id == draft.CondominiumId))
        {
            session.Restart(now, expires);
            return new("A confirmação não é mais válida. Nenhuma alteração foi realizada.", "admin_confirmation_stale");
        }
        var data = draft.Extraction;
        if (!TryRelationship(data.Relationship, out var relationship))
            return new("A confirmação contém uma relação inválida. Envie “Cadastrar morador” para recomeçar.", "admin_confirmation_invalid");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var user = draft.ExistingUserId is Guid id
                ? await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct)
                : null;
            var isNew = user is null;
            if (user is null)
            {
                user = new ApplicationUser(data.FullName!, data.Email!, data.Phone);
                user.RequirePasswordChange();
                var created = await userManager.CreateAsync(user, GeneratePassword());
                if (!created.Succeeded) throw new InvalidOperationException("Identity rejected resident creation.");
            }
            if (await db.UnitMemberships.AnyAsync(x => x.UserId == user.Id
                    && x.UnitId == draft.UnitId && x.IsActive, ct))
            {
                await transaction.RollbackAsync(ct);
                session.Restart(now, expires);
                return new($"{user.FullName} já está vinculado à unidade {draft.UnitDisplay}.", "admin_unit_membership_duplicate");
            }
            var membership = await db.CondominiumMemberships.SingleOrDefaultAsync(x =>
                x.UserId == user.Id && x.CondominiumId == draft.CondominiumId, ct);
            if (membership is { IsActive: false }) throw new InvalidOperationException("Inactive condominium membership cannot be reused.");
            if (membership is null)
            {
                membership = new CondominiumMembership(user.Id, draft.CondominiumId.Value);
                db.CondominiumMemberships.Add(membership);
            }
            var role = await db.CondominiumMembershipRoles.SingleOrDefaultAsync(x =>
                x.CondominiumMembershipId == membership.Id && x.Role == CondominiumRole.Resident, ct);
            if (role is { IsActive: false }) throw new InvalidOperationException("Inactive resident role cannot be reused.");
            if (role is null) db.CondominiumMembershipRoles.Add(
                new CondominiumMembershipRole(membership.Id, CondominiumRole.Resident));
            db.UnitMemberships.Add(new UnitMembership(user.Id, draft.UnitId.Value, relationship,
                true, data.IsPrimaryResidence ?? false));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            session.End(now);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Administrative WhatsApp mutation completed. AdministratorUserId: {AdministratorUserId}; Action: {Action}; AffectedUserId: {AffectedUserId}; CondominiumId: {CondominiumId}; UnitId: {UnitId}; IsNewUser: {IsNewUser}; OccurredAt: {OccurredAt}.",
                administrator.Id, "RegisterResident", user.Id, draft.CondominiumId, draft.UnitId, isNew, now);
            return new($"Cadastro concluído.\n\n{user.FullName} foi vinculado à unidade {draft.UnitDisplay}."
                + (data.Phone is null ? "" : "\n\nO telefone já está habilitado para usar o WhatsApp do Comvy."),
                "admin_registration_completed");
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(ct);
            logger.LogWarning("Administrative WhatsApp mutation failed. AdministratorUserId: {AdministratorUserId}; Action: {Action}; CondominiumId: {CondominiumId}; UnitId: {UnitId}; FailureType: {FailureType}.",
                administrator.Id, "RegisterResident", draft.CondominiumId, draft.UnitId, exception.GetType().Name);
            throw;
        }
    }

    private async Task<IReadOnlyList<ScopedCondominium>> AdministrativeScope(Guid userId, CancellationToken ct)
    {
        var platformAdmin = await db.UserRoles.AsNoTracking().Join(db.Roles.AsNoTracking(),
            link => link.RoleId, role => role.Id, (link, role) => new { link.UserId, role.NormalizedName })
            .AnyAsync(x => x.UserId == userId && x.NormalizedName == DependencyInjection.PlatformAdminRole.ToUpper(), ct);
        if (platformAdmin)
            return await db.Condominiums.AsNoTracking().Where(x => x.IsActive)
                .OrderBy(x => x.Name).Select(x => new ScopedCondominium(x.Id, x.Name)).ToArrayAsync(ct);
        return await (from membership in db.CondominiumMemberships.AsNoTracking()
            join role in db.CondominiumMembershipRoles.AsNoTracking() on membership.Id equals role.CondominiumMembershipId
            join condominium in db.Condominiums.AsNoTracking() on membership.CondominiumId equals condominium.Id
            where membership.UserId == userId && membership.IsActive && membership.EndedAt == null
                && role.Role == CondominiumRole.Manager && role.IsActive && role.RevokedAt == null && condominium.IsActive
            orderby condominium.Name select new ScopedCondominium(condominium.Id, condominium.Name))
            .Distinct().ToArrayAsync(ct);
    }

    private static AdministrativeWhatsAppResponse Missing(WhatsAppSession session, AdminDraft draft,
        string prompt, DateTime now, DateTime expires)
    {
        session.SetAdministrativeDraft(JsonSerializer.Serialize(draft, JsonOptions),
            WhatsAppConversationState.CollectingAdminResidentData, now, expires, draft.CondominiumId);
        return new(prompt + "\n\n0 - Cancelar", "admin_registration_missing_data");
    }
    private static bool IsCommand(string? text)
    {
        var value = text?.Trim().ToLowerInvariant();
        return value is not null && (value == "cadastrar morador" || value == "novo morador"
            || value.StartsWith("cadastrar morador\n") || value.StartsWith("cadastre "));
    }
    private static bool Equivalent(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool TryRelationship(string? value, out UnitRelationshipType relationship)
    {
        relationship = value?.Trim().ToLowerInvariant() switch
        {
            "owner" or "proprietário" or "proprietario" or "1" => UnitRelationshipType.Owner,
            "tenant" or "inquilino" or "2" => UnitRelationshipType.Tenant,
            "authorizedoccupant" or "morador autorizado" or "ocupante autorizado" or "3" => UnitRelationshipType.AuthorizedOccupant,
            _ => 0
        };
        return relationship != 0;
    }
    private static AdministrativeResidentExtraction Merge(AdministrativeResidentExtraction? old,
        AdministrativeResidentExtraction next) => new("register_resident",
        next.FullName ?? old?.FullName, next.Phone ?? old?.Phone, next.Email ?? old?.Email,
        next.Condominium ?? old?.Condominium, next.Block ?? old?.Block, next.Unit ?? old?.Unit,
        next.Relationship ?? old?.Relationship, true,
        next.IsPrimaryResidence ?? old?.IsPrimaryResidence ?? false);
    private static AdminDraft ReadDraft(string? json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<AdminDraft>(json, JsonOptions) ?? new(); }
        catch (JsonException) { return new(); }
    }
    private static string UnitChoicesPrompt(IReadOnlyList<UnitChoice> choices) =>
        "Encontrei mais de uma unidade:\n\n" + string.Join("\n", choices.Select((x, i) => $"{i + 1} - {x.Display}")) + "\n0 - Cancelar";
    private static string Confirmation(AdminDraft draft)
    {
        var d = draft.Extraction!;
        var existing = draft.ExistingUserId is null ? "Vou cadastrar:" : $"Já encontrei {draft.ExistingUserName} no Comvy. Vou apenas vinculá-lo à unidade.";
        return $"{existing}\n\n{d.FullName}\nTelefone: {d.Phone}\nE-mail: {d.Email}\n\nCondomínio: {draft.CondominiumName}\nUnidade: {draft.UnitDisplay}\nRelação: {RelationshipLabel(d.Relationship)}\nResidência principal: {(d.IsPrimaryResidence == true ? "Sim" : "Não")}\n\n1 - Confirmar\n2 - Corrigir\n0 - Cancelar";
    }
    private static string RelationshipLabel(string? value) => TryRelationship(value, out var parsed) ? parsed switch
    { UnitRelationshipType.Owner => "Proprietário", UnitRelationshipType.Tenant => "Inquilino", _ => "Ocupante autorizado" } : value ?? "";
    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz", digits = "23456789";
        var all = upper + lower + digits; var chars = new char[14];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < chars.Length; i++) chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }
    private sealed record ScopedCondominium(Guid Id, string Name);
    private sealed record UnitChoice(Guid Id, string Display);
    private sealed record AdminDraft(AdministrativeResidentExtraction? Extraction = null,
        Guid? CondominiumId = null, string? CondominiumName = null, Guid? UnitId = null,
        string? UnitDisplay = null, UnitChoice[]? Choices = null, Guid? ExistingUserId = null,
        string? ExistingUserName = null)
    {
        public UnitChoice[] UnitChoices { get; init; } = Choices ?? [];
    }
}
