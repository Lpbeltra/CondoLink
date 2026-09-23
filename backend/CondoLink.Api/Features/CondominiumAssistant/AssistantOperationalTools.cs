using System.Text.Json;
using CondoLink.Api.Features.Management;
using CondoLink.Api.Features.Requests;
using CondoLink.Api.Features.Agenda;
using CondoLink.Api.Features.CondominiumMembers;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.CondominiumAssistant;

public sealed record AssistantOperationalReference(string Type, Guid Id, string Label, string? Href);
public sealed record AssistantToolResult(string Json, IReadOnlyList<AssistantOperationalReference> References,
    bool Succeeded, Guid? PendingActionId = null, AssistantResidentPreview? ResidentPreview = null);

/// <summary>Read-only operational tool registry. Scope comes from conversation, never model arguments.</summary>
public sealed class AssistantOperationalTools(AppDbContext db, ILogger<AssistantOperationalTools> logger,
    IOptions<AgendaOptions> agendaOptions, AssistantResidentRegistrationService? residentRegistration = null)
{
    public const int MaxToolCalls = 6;
    private const int MaxRows = 20;
    private static readonly IReadOnlyList<object> ReadOnlyDefinitions =
    [
        Function("search_residents", "Fonte operacional autoritativa. Localiza moradores por nome ou telefone; use unit para localizar por unidade. Use para encontrar um morador, nao para consultar regras ou documentos.", new { type = "object", properties = new { query = new { type = "string" }, unit = new { type = "string" } }, additionalProperties = false }),
        Function("get_unit_residents", "Fonte operacional autoritativa para fatos da unidade: quem mora, proprietario, ocupantes e moradores do apartamento informado. Use sempre que a pergunta pedir moradores de uma unidade. Nao use RAG ou documentos para esse fato.", new { type = "object", properties = new { unit = new { type = "string", description = "Identificador da unidade, por exemplo 1201 ou 206." } }, required = new[] { "unit" }, additionalProperties = false }),
        Function("search_requests", "Fonte operacional autoritativa para atendimentos do condomínio atual. Use para perguntas sobre atendimentos de uma unidade, inclusive quando a unidade foi mencionada antes na conversa; nesse caso, resolva a referência conversacional e informe unit. Para atendimentos abertos, use status open: inclui todos os estados não encerrados. Para estado específico, use InProgress, WaitingForResident, WaitingForThirdParty, WaitingForManager, WaitingForResidentClosure, Resolved ou Cancelled.", new { type = "object", properties = new { query = new { type = "string" }, status = new { type = "string" }, priority = new { type = "string" }, unit = new { type = "string" } }, additionalProperties = false }),
        Function("get_request", "Obtém resumo e histórico limitado de atendimento autorizado.", new { type = "object", properties = new { protocol = new { type = "string" }, id = new { type = "string" } }, additionalProperties = false }),
        Function("list_reminders", "Consulta lembretes da Agenda do condomínio atual. Use view today, overdue, week ou recurring.", new { type = "object", properties = new { view = new { type = "string" }, query = new { type = "string" } }, additionalProperties = false }),
        Function("search_service_providers", "Busca prestadores ativos visíveis ao usuário no condomínio atual.", new { type = "object", properties = new { query = new { type = "string" }, includePix = new { type = "boolean" } }, additionalProperties = false }),
        Function("get_management_company", "Consulta administradora atualmente vinculada e contatos/setores existentes.", new { type = "object", properties = new { }, additionalProperties = false }),
        Function("search_management_company_requests", "Lista solicitações abertas da administradora no condomínio atual.", new { type = "object", properties = new { query = new { type = "string" }, status = new { type = "string" } }, additionalProperties = false })
    ];

    private static object Function(string name, string description, object parameters) => new
    { type = "function", function = new { name, description, parameters } };

    public IReadOnlyList<object> GetDefinitions(CondominiumAssistantChannel channel) =>
        channel == CondominiumAssistantChannel.Telegram
            ? [.. ReadOnlyDefinitions, PrepareResidentRegistrationDefinition]
            : ReadOnlyDefinitions;

    private static readonly object PrepareResidentRegistrationDefinition = Function("prepare_resident_registration",
        "Prepara, sem executar, o cadastro de um morador no Telegram. Use quando o usuário pedir cadastro/registro e todos os dados obrigatórios explícitos estiverem disponíveis: nome, email, unidade e vínculo. Continue essa intenção quando o usuário complementar ou corrigir dados nos turnos seguintes. Nunca inferir vínculo; use Owner, Tenant ou AuthorizedOccupant. Não inclua IDs, condomínio, ator, chat ou contexto técnico.",
        new { type = "object", properties = new { fullName = new { type = "string" }, email = new { type = "string" }, phoneNumber = new { type = "string" }, unitIdentifier = new { type = "string", description = "Somente o identificador da unidade, por exemplo 1201; nunca inclua 'bloco 1' neste campo." }, blockIdentifier = new { type = "string", description = "Identificador do bloco separado da unidade, por exemplo 1 em 'unidade 1201 bloco 1'." }, relationshipType = new { type = "string", @enum = new[] { "Owner", "Tenant", "AuthorizedOccupant" } }, isPrimaryResidence = new { type = "boolean" } }, required = new[] { "fullName", "email", "unitIdentifier", "relationshipType" }, additionalProperties = false });

    public async Task<AssistantToolResult> ExecuteAsync(string name, string arguments, Guid userId, Guid condominiumId, CancellationToken ct,
        string channel = "Unknown", IReadOnlyCollection<Guid>? conversationUnitIds = null,
        string? externalContextId = null, string? idempotencyKey = null)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        if (name != "prepare_resident_registration" && !await CanReadAsync(name, userId, condominiumId, ct))
        {
            logger.LogInformation("Assistant tool denied. Tool: {Tool}; Channel: {Channel}; CondominiumId: {CondominiumId}.", name, channel, condominiumId);
            return Error("Consulta não autorizada.");
        }
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments);
            var argumentFields = document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.EnumerateObject().Select(x => x.Name).Order().ToArray() : [];
            var result = name switch
            {
                "search_residents" => await SearchResidents(document.RootElement, condominiumId, ct),
                "get_unit_residents" => await UnitResidents(document.RootElement, condominiumId, ct),
                "search_requests" => await SearchRequests(document.RootElement, condominiumId, ct, conversationUnitIds),
                "get_request" => await GetRequest(document.RootElement, condominiumId, ct),
                "list_reminders" => await ListReminders(document.RootElement, condominiumId, ct),
                "search_service_providers" => await SearchProviders(document.RootElement, userId, condominiumId, ct),
                "get_management_company" => await ManagementCompany(condominiumId, ct),
                "search_management_company_requests" => await SearchManagementCompanyRequests(document.RootElement, condominiumId, ct),
                "prepare_resident_registration" => await PrepareResidentRegistration(document.RootElement, userId, condominiumId, channel, externalContextId, idempotencyKey, ct),
                _ => Error("Tool inexistente.")
            };
            logger.LogInformation("Assistant tool completed. Tool: {Tool}; Channel: {Channel}; CondominiumId: {CondominiumId}; ArgumentFields: {@ArgumentFields}; ResultKind: {ResultKind}; Results: {Results}; ReferenceTypes: {@ReferenceTypes}; DurationMs: {DurationMs}.",
                name, channel, condominiumId, argumentFields, ResultKind(result.Json), result.References.Count,
                result.References.Select(x => x.Type).Distinct().Order().ToArray(), started.ElapsedMilliseconds);
            return result;
        }
        catch (JsonException)
        {
            logger.LogInformation("Assistant tool rejected invalid arguments. Tool: {Tool}; Channel: {Channel}; CondominiumId: {CondominiumId}; ArgumentsPresent: {ArgumentsPresent}.",
                name, channel, condominiumId, !string.IsNullOrWhiteSpace(arguments));
            return Error("Argumentos inválidos.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { logger.LogWarning("Assistant tool failed. Tool: {Tool}; CondominiumId: {CondominiumId}; FailureType: {FailureType}.", name, condominiumId, ex.GetType().Name); return Error("Não foi possível consultar este domínio."); }
    }

    private async Task<AssistantToolResult> PrepareResidentRegistration(JsonElement a, Guid actor, Guid condo, string channel,
        string? externalContextId, string? idempotencyKey, CancellationToken ct)
    {
        if (residentRegistration is null || !Enum.TryParse<CondominiumAssistantChannel>(channel, out var parsedChannel)
            || parsedChannel != CondominiumAssistantChannel.Telegram || string.IsNullOrWhiteSpace(externalContextId) || string.IsNullOrWhiteSpace(idempotencyKey))
            return Error("Não foi possível preparar este cadastro agora.");
        var prepared = await residentRegistration.PrepareAsync(new(actor, condo, parsedChannel, externalContextId, idempotencyKey,
            Text(a, "fullName"), Text(a, "email"), Text(a, "phoneNumber"), Text(a, "unitIdentifier"), Text(a, "blockIdentifier"),
            Text(a, "relationshipType"), Bool(a, "isPrimaryResidence")), ct);
        if (prepared.ReadyToPreview && prepared.ActionId is Guid actionId && prepared.Preview is not null)
            return new(JsonSerializer.Serialize(new { status = "prepared", preview = new { prepared.Preview.FullName, prepared.Preview.Unit, prepared.Preview.Email, prepared.Preview.PhoneNumber, prepared.Preview.RelationshipType } }), [], true, actionId, prepared.Preview);
        if (prepared.MissingFields is { Count: > 0 }) return Error($"Dados necessários: {string.Join(", ", prepared.MissingFields)}.");
        if (prepared.UnitOptions is { Count: > 0 }) return Error($"Informe o bloco da unidade: {string.Join("; ", prepared.UnitOptions)}.");
        return Error(prepared.Error switch { "UnitNotFound" => "Unidade não encontrada. Confira bloco e unidade.", "Forbidden" => "Não foi possível preparar este cadastro.", _ => "Confira os dados do cadastro e tente novamente." });
    }

    private async Task<bool> CanReadAsync(string tool, Guid userId, Guid condo, CancellationToken ct)
    {
        var module = tool switch
        {
            "list_reminders" => SubManagerModule.Agenda,
            "get_management_company" or "search_management_company_requests" => SubManagerModule.ManagementCompany,
            "search_requests" or "get_request" => SubManagerModule.Attendance,
            _ => SubManagerModule.Management
        };
        return await SubManagerAccess.HasAsync(db, userId, condo, module, ct);
    }

    private async Task<AssistantToolResult> SearchResidents(JsonElement a, Guid condo, CancellationToken ct)
    {
        var query = Text(a, "query"); var unit = Text(a, "unit");
        var rows = await (from m in db.UnitMemberships.AsNoTracking()
                          join u in db.Units.AsNoTracking() on m.UnitId equals u.Id
                          join p in db.Users.AsNoTracking() on m.UserId equals p.Id
                          join b in db.CondominiumBlocks.AsNoTracking() on u.BlockId equals b.Id into blocks
                          from b in blocks.DefaultIfEmpty()
                          where u.CondominiumId == condo && u.IsActive && m.IsActive && m.IsResident
                          && (string.IsNullOrWhiteSpace(query) || p.FullName.ToLower().Contains(query.ToLower()) || (p.PhoneNumber != null && p.PhoneNumber.Contains(query)))
                          && (string.IsNullOrWhiteSpace(unit) || u.Identifier.ToLower().Contains(unit.ToLower()))
                          orderby p.FullName select new { UnitId = u.Id, p.FullName, p.PhoneNumber, p.Email, Unit = u.Identifier, Block = b == null ? null : b.Identifier, Relationship = m.RelationshipType.ToString() }).Take(MaxRows).ToArrayAsync(ct);
        return Rows(rows, rows.Select(x => new AssistantOperationalReference("unit", x.UnitId, $"{x.Block} · {x.Unit}", $"/management/units/{x.UnitId}")).ToArray());
    }

    private async Task<AssistantToolResult> UnitResidents(JsonElement a, Guid condo, CancellationToken ct)
    {
        var unit = Text(a, "unit");
        var rows = await (from m in db.UnitMemberships.AsNoTracking()
                          join u in db.Units.AsNoTracking() on m.UnitId equals u.Id
                          join p in db.Users.AsNoTracking() on m.UserId equals p.Id
                          where u.CondominiumId == condo && u.IsActive && m.IsActive && m.IsResident && u.Identifier.ToLower() == unit.ToLower()
                          select new { UnitId = u.Id, p.FullName, p.PhoneNumber, p.Email, Unit = u.Identifier, Relationship = m.RelationshipType.ToString() }).Take(MaxRows).ToArrayAsync(ct);
        var reference = rows.FirstOrDefault() is { } first
            ? new AssistantOperationalReference("unit", first.UnitId, $"Unidade {first.Unit}", $"/management/units/{first.UnitId}")
            : null;
        return Rows(rows, reference is null ? [] : [reference]);
    }

    private async Task<AssistantToolResult> SearchRequests(JsonElement a, Guid condo, CancellationToken ct,
        IReadOnlyCollection<Guid>? conversationUnitIds)
    {
        var q = Text(a, "query"); var status = Text(a, "status"); var priority = Text(a, "priority"); var unit = Text(a, "unit");
        var openStatusFilter = status.Equals("open", StringComparison.OrdinalIgnoreCase);
        var parsedStatus = !openStatusFilter && Enum.TryParse<RequestStatus>(status, true, out var statusValue) ? statusValue : (RequestStatus?)null;
        var parsedPriority = Enum.TryParse<RequestPriority>(priority, true, out var priorityValue) ? priorityValue : (RequestPriority?)null;
        var matchedUnitIds = string.IsNullOrWhiteSpace(unit) ? [] : await db.Units.AsNoTracking()
            .Where(u => u.CondominiumId == condo && u.Identifier.ToLower().Contains(unit.ToLower()))
            .Select(u => u.Id).ToArrayAsync(ct);
        var matchesConversationUnitContext = conversationUnitIds is { Count: > 0 }
            && matchedUnitIds.Any(conversationUnitIds.Contains);
        logger.LogInformation("Assistant attendance filter resolved. CondominiumId: {CondominiumId}; UnitFilterPresent: {UnitFilterPresent}; MatchingUnits: {MatchingUnits}; MatchesConversationUnitContext: {MatchesConversationUnitContext}; StatusFilterPresent: {StatusFilterPresent}; StatusRecognized: {StatusRecognized}; PriorityFilterPresent: {PriorityFilterPresent}; PriorityRecognized: {PriorityRecognized}; QueryFilterPresent: {QueryFilterPresent}.",
            condo, !string.IsNullOrWhiteSpace(unit), matchedUnitIds.Length, matchesConversationUnitContext,
            !string.IsNullOrWhiteSpace(status), openStatusFilter || parsedStatus is not null, !string.IsNullOrWhiteSpace(priority), parsedPriority is not null, !string.IsNullOrWhiteSpace(q));
        var query = RequestsFor(condo);
        if (openStatusFilter) query = query.Where(x => x.Status != RequestStatus.Resolved && x.Status != RequestStatus.Cancelled);
        else if (parsedStatus is RequestStatus parsed) query = query.Where(x => x.Status == parsed);
        if (parsedPriority is RequestPriority priorityParsed) query = query.Where(x => x.Priority == priorityParsed);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Title.ToLower().Contains(q.ToLower()) || x.Description.ToLower().Contains(q.ToLower()));
        if (!string.IsNullOrWhiteSpace(unit)) query = query.Where(x => x.TargetUnitId != null && db.Units.Any(u => u.Id == x.TargetUnitId && u.Identifier.ToLower().Contains(unit.ToLower())));
        var rows = await query.OrderByDescending(x => x.UpdatedAt).Take(MaxRows).Select(x => new { x.Id, Protocol = RequestProtocol.From(x.Id), x.Title, x.Description, Status = x.Status.ToString(), Priority = x.Priority.ToString(), x.CreatedAt, x.UpdatedAt, Unit = db.Units.Where(u => u.Id == x.TargetUnitId).Select(u => u.Identifier).FirstOrDefault(), Resident = db.Users.Where(u => u.Id == x.AuthorUserId).Select(u => u.FullName).FirstOrDefault() }).ToArrayAsync(ct);
        return Rows(rows, rows.Select(x => new AssistantOperationalReference("request", x.Id, $"Atendimento #{x.Protocol}", $"/requests/{x.Id}")).ToArray());
    }

    private async Task<AssistantToolResult> GetRequest(JsonElement a, Guid condo, CancellationToken ct)
    {
        var value = Text(a, "id"); if (string.IsNullOrWhiteSpace(value)) value = Text(a, "protocol");
        var candidates = await RequestsFor(condo).OrderByDescending(x => x.UpdatedAt).Take(200).Select(x => x.Id).ToArrayAsync(ct);
        var requestId = candidates.FirstOrDefault(id => id.ToString().Equals(value, StringComparison.OrdinalIgnoreCase)
            || RequestProtocol.From(id).Equals(value.TrimStart('#'), StringComparison.OrdinalIgnoreCase));
        if (requestId == Guid.Empty) return Error("Atendimento nÃ£o encontrado.");
        var request = await RequestsFor(condo).Where(x => x.Id == requestId).Select(x => new { x.Id, Protocol = RequestProtocol.From(x.Id), x.Title, x.Description, Status = x.Status.ToString(), Priority = x.Priority.ToString(), x.CreatedAt, x.UpdatedAt, Unit = db.Units.Where(u => u.Id == x.TargetUnitId).Select(u => u.Identifier).FirstOrDefault(), Resident = db.Users.Where(u => u.Id == x.AuthorUserId).Select(u => u.FullName).FirstOrDefault(), Category = db.Categories.Where(c => c.Id == x.CategoryId).Select(c => c.Name).FirstOrDefault() }).SingleOrDefaultAsync(ct);
        if (request is null) return Error("Atendimento não encontrado.");
        var messages = await db.RequestMessages.AsNoTracking().Where(x => x.RequestId == request.Id).OrderByDescending(x => x.CreatedAt).Take(8).OrderBy(x => x.CreatedAt).Select(x => new { x.Content, x.CreatedAt }).ToArrayAsync(ct);
        var history = await db.RequestStatusHistories.AsNoTracking().Where(x => x.RequestId == request.Id).OrderByDescending(x => x.CreatedAt).Take(8).OrderBy(x => x.CreatedAt).Select(x => new { Status = x.NewStatus.ToString(), x.Reason, x.CreatedAt }).ToArrayAsync(ct);
        return Rows(new { request, messages, history }, [new("request", request.Id, $"Atendimento #{request.Protocol}", $"/requests/{request.Id}")]);
    }

    private async Task<AssistantToolResult> ListReminders(JsonElement a, Guid condo, CancellationToken ct)
    {
        var view = Text(a, "view").ToLowerInvariant(); var q = Text(a, "query"); var now = DateTime.UtcNow;
        var query = db.AgendaReminders.AsNoTracking().Where(x => x.CondominiumId == condo);
        if (view == "recurring") query = query.Where(x => x.IsActive && x.RecurrenceType != AgendaRecurrenceType.None);
        else if (view == "overdue") query = query.Where(x => x.IsActive && x.NextOccurrenceAtUtc < now);
        else if (view is "today" or "tomorrow" or "week")
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(agendaOptions.Value.OperationalTimeZone);
            var local = TimeZoneInfo.ConvertTimeFromUtc(now, zone);
            var startLocal = local.Date.AddDays(view == "tomorrow" ? 1 : 0);
            var endLocal = view == "week" ? local.Date.AddDays(7) : startLocal.AddDays(1);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified), zone);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified), zone);
            query = query.Where(x => x.IsActive && x.NextOccurrenceAtUtc >= startUtc && x.NextOccurrenceAtUtc < endUtc);
        }
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Title.ToLower().Contains(q.ToLower()) || (x.RelatedThirdParty != null && x.RelatedThirdParty.ToLower().Contains(q.ToLower())));
        var rows = await query.OrderBy(x => x.NextOccurrenceAtUtc).Take(MaxRows).Select(x => new { x.Id, x.Title, x.Description, x.NextOccurrenceAtUtc, x.TimeZoneId, x.RecurrenceType, x.IsActive, x.CompletedAt, LinkedRequests = db.AgendaReminderRequests.Where(l => l.ReminderId == x.Id).Select(l => l.RequestId).Take(10).ToArray() }).ToArrayAsync(ct);
        return Rows(rows, rows.Select(x => new AssistantOperationalReference("reminder", x.Id, $"Lembrete: {x.Title}", $"/management/agenda?reminderId={x.Id}")).ToArray());
    }

    private async Task<AssistantToolResult> SearchProviders(JsonElement a, Guid userId, Guid condo, CancellationToken ct)
    {
        var q = Text(a, "query"); var includePix = a.TryGetProperty("includePix", out var p) && p.ValueKind == JsonValueKind.True;
        var rows = await db.ServiceProviders.AsNoTracking().Where(x => x.IsActive && (db.ServiceProviderCondominiumLinks.Any(l => l.ServiceProviderId == x.Id && l.CondominiumId == condo) || db.ServiceProviderUserLinks.Any(l => l.ServiceProviderId == x.Id && l.UserId == userId)) && (string.IsNullOrWhiteSpace(q) || x.Name.ToLower().Contains(q.ToLower()) || x.Specialty.ToLower().Contains(q.ToLower()) || x.Phone.Contains(q)))
            .OrderBy(x => x.Name).Take(MaxRows).Select(x => new { x.Id, x.Name, x.CompanyName, x.Specialty, x.Phone, x.Email, Pix = includePix ? x.PixKey : null, PixType = includePix ? x.PixKeyType.ToString() : null }).ToArrayAsync(ct);
        return Rows(rows, rows.Select(x => new AssistantOperationalReference("service_provider", x.Id, x.Name, null)).ToArray());
    }

    private async Task<AssistantToolResult> ManagementCompany(Guid condo, CancellationToken ct)
    {
        var row = await (from c in db.Condominiums.AsNoTracking() join m in db.ManagementCompanies.AsNoTracking() on c.ManagementCompanyId equals m.Id where c.Id == condo && c.ManagementCompanyId != null && m.IsActive select new { m.Id, m.Name, m.Email, m.PhoneNumber, Employees = db.ManagementCompanyEmployees.Where(e => e.ManagementCompanyId == m.Id && e.IsActive).Select(e => new { e.JobTitle, e.AccessType, Name = db.Users.Where(u => u.Id == e.UserId).Select(u => u.FullName).FirstOrDefault() }).Take(MaxRows).ToArray() }).SingleOrDefaultAsync(ct);
        return row is null ? Error("Não há administradora vinculada.") : Rows(row, [new("management_company", row.Id, row.Name, null)]);
    }

    private async Task<AssistantToolResult> SearchManagementCompanyRequests(JsonElement a, Guid condo, CancellationToken ct)
    {
        var q = Text(a, "query");
        var status = Text(a, "status");
        var query = db.ManagementCompanyRequests.AsNoTracking().Where(x => x.CondominiumId == condo
            && x.Status != ManagementCompanyRequestStatus.Completed
            && x.Status != ManagementCompanyRequestStatus.Cancelled);
        if (Enum.TryParse<ManagementCompanyRequestStatus>(status, true, out var parsed)) query = query.Where(x => x.Status == parsed);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.FriendlyIdentifier.Contains(q));
        var rows = await query.OrderByDescending(x => x.UpdatedAt).Take(MaxRows).Select(x => new
        {
            x.Id, x.FriendlyIdentifier, x.Type, x.Status, x.CreatedAt, x.UpdatedAt,
            Subject = x.Type == ManagementCompanyRequestType.Fine
                ? db.ManagementCompanyFineRequests.Where(y => y.RequestId == x.Id).Select(y => y.Nature).FirstOrDefault()
                : x.Type == ManagementCompanyRequestType.Payment
                    ? db.ManagementCompanyPaymentRequests.Where(y => y.RequestId == x.Id).Select(y => y.Nature).FirstOrDefault()
                    : db.ManagementCompanyGeneralQuestionRequests.Where(y => y.RequestId == x.Id).Select(y => y.Theme).FirstOrDefault()
        }).ToArrayAsync(ct);
        return Rows(rows, rows.Select(x => new AssistantOperationalReference("management_company_request", x.Id,
            $"Solicitação {x.FriendlyIdentifier}", $"/management-company-requests/{x.Id}")).ToArray());
    }

    private IQueryable<CondoLink.Domain.Entities.Request> RequestsFor(Guid condo) =>
        db.Requests.AsNoTracking().Where(x => x.CondominiumId == condo);
    private static string Text(JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()?.Trim() ?? "" : "";
    private static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False && p.GetBoolean();
    private static string ResultKind(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("error", out _) ? "error" : "rows";
    }
    private static AssistantToolResult Error(string message) => new(JsonSerializer.Serialize(new { error = message }), [], false);
    private static AssistantToolResult Rows<T>(T rows, IReadOnlyList<AssistantOperationalReference> refs) => new(JsonSerializer.Serialize(new { rows, limit = MaxRows, hasMore = refs.Count >= MaxRows }), refs, true);
}
