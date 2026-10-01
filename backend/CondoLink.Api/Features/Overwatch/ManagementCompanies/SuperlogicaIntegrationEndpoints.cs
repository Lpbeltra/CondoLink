using System.Net;
using System.Net.Http.Headers;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using CondoLink.Domain;
using CondoLink.Api.Features.Overwatch.Condominiums;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public interface ISuperlogicaClient
{
    Task<SuperlogicaValidationResult> ValidateAsync(string appToken, string accessToken, CancellationToken cancellationToken);
    Task<SuperlogicaCondominiumListResult> ListCondominiumsAsync(string appToken, string accessToken, CancellationToken cancellationToken);
    Task<SuperlogicaUnitPageResult> ListUnitsPageAsync(string appToken, string accessToken, string externalCondominiumId, int page, CancellationToken cancellationToken);
}

public sealed record SuperlogicaValidationResult(bool Success, bool InvalidCredentials);
public sealed record SuperlogicaCondominium(string ExternalId, string Name, string? TradeName, string? TaxId, string? Address, string? AddressComplement, string? Neighborhood, string? City, string? State, string? ZipCode);
public sealed record SuperlogicaCondominiumListResult(IReadOnlyList<SuperlogicaCondominium>? Condominiums, bool InvalidCredentials);
public sealed record SuperlogicaUnitPageResult(IReadOnlyList<SuperlogicaUnitRow>? Rows, bool InvalidCredentials);
public sealed record SuperlogicaUnitRow(string? ExternalUnitId, string? Identifier, string? Block,
    string? ExternalContactId, string? Name, string? Email, string? Phone, string? Fax, string? TaxId,
    string? RelationshipType, string? EntryDate, string? ExitDate,
    string? ExternalOwnerId, string? OwnerName, string? OwnerEmail, string? OwnerPhone, string? OwnerMobile, string? OwnerTaxId, string? OwnerType);

public sealed class SuperlogicaClient(HttpClient httpClient) : ISuperlogicaClient
{
    private const string CondominiumsUri = "/v2/condor/condominios/get?id=-1&somenteCondominiosAtivos=1&ignorarCondominioModelo=1&apenasColunasPrincipais=1&apenasDadosDoPlanoDeContas=0&comDataFechamento=1&itensPorPagina=50&pagina=1";

    public async Task<SuperlogicaValidationResult> ValidateAsync(string appToken, string accessToken, CancellationToken cancellationToken)
    {
        using var request = CreateCondominiumsRequest(appToken, accessToken);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return response.IsSuccessStatusCode
            ? new(true, false)
            : response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? new(false, true) : new(false, false);
    }

    public async Task<SuperlogicaCondominiumListResult> ListCondominiumsAsync(string appToken, string accessToken, CancellationToken cancellationToken)
    {
        using var request = CreateCondominiumsRequest(appToken, accessToken);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new(null, true);
        if (!response.IsSuccessStatusCode) return new(null, false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var rows = await JsonSerializer.DeserializeAsync<List<SuperlogicaCondominiumDto>>(stream, JsonOptions, cancellationToken);
        return rows is null ? new(null, false) : new(rows.Where(x => !string.IsNullOrWhiteSpace(x.ExternalIdValue)).Select(x => x.ToModel()).ToArray(), false);
    }

    public async Task<SuperlogicaUnitPageResult> ListUnitsPageAsync(string appToken, string accessToken, string externalCondominiumId, int page, CancellationToken cancellationToken)
    {
        var uri = $"/v2/condor/unidades/index?idCondominio={Uri.EscapeDataString(externalCondominiumId)}&exibirDadosDosContatos=1&exibirGruposDasUnidades=0&exibirInadimplencia=0&itensPorPagina=50&pagina={page}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("app_token", appToken);
        request.Headers.TryAddWithoutValidation("access_token", accessToken);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return new(null, true);
        if (!response.IsSuccessStatusCode) return new(null, false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var rowsElement = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : TryGetArray(document.RootElement, "data", "items", "dados");
        if (rowsElement.ValueKind != JsonValueKind.Array) return new(null, false);
        var rows = rowsElement.EnumerateArray().Select(ReadUnitRow).ToArray();
        return new(rows, false);
    }

    private static JsonElement TryGetArray(JsonElement root, params string[] names)
    {
        foreach (var name in names)
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array) return value;
        return default;
    }

    private static HttpRequestMessage CreateCondominiumsRequest(string appToken, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, CondominiumsUri)
        {
            Content = new ByteArrayContent([])
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation("app_token", appToken);
        request.Headers.TryAddWithoutValidation("access_token", accessToken);
        return request;
    }

    private static SuperlogicaUnitRow ReadUnitRow(JsonElement row) => new(
        ReadValue(row, "id_unidade_uni"), Clean(ReadValue(row, "st_unidade_uni")), Clean(ReadValue(row, "st_bloco_uni")),
        ReadValue(row, "id_contato_con"), Clean(ReadValue(row, "st_nome_con")), NormalizeEmail(ReadValue(row, "st_email_con")),
        ReadValue(row, "st_telefone_con"), ReadValue(row, "st_fax_con"), RegistrationData.Digits(ReadValue(row, "st_cpf_con")),
        ReadValue(row, "id_tiporesp_tres"), Clean(ReadValue(row, "dt_entrada_res")), Clean(ReadValue(row, "dt_saida_res")),
        ReadValue(row, "id_proprietario"), Clean(ReadValue(row, "nome_proprietario")), NormalizeEmail(ReadValue(row, "email_proprietario")),
        ReadValue(row, "telefone_proprietario"), ReadValue(row, "celular_proprietario"), RegistrationData.Digits(ReadValue(row, "cpf_proprietario")),
        ReadValue(row, "tipo_proprietario"));

    private static string? ReadValue(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => Clean(value.GetString()),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static string? Clean(string? value) => RegistrationData.Optional(value);
    private static string? NormalizeEmail(string? value) => Clean(value)?.ToLowerInvariant();

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class SuperlogicaCondominiumDto
    {
        [JsonPropertyName("id_condominio_cond")] public JsonElement ExternalId { get; set; }
        [JsonPropertyName("st_nome_cond")] public string? Name { get; set; }
        [JsonPropertyName("st_fantasia_cond")] public string? TradeName { get; set; }
        [JsonPropertyName("st_cpf_cond")] public string? TaxId { get; set; }
        [JsonPropertyName("st_endereco_cond")] public string? Address { get; set; }
        [JsonPropertyName("st_complemento_cond")] public string? AddressComplement { get; set; }
        [JsonPropertyName("st_bairro_cond")] public string? Neighborhood { get; set; }
        [JsonPropertyName("st_cidade_cond")] public string? City { get; set; }
        [JsonPropertyName("st_uf_uf")] public string? State { get; set; }
        [JsonPropertyName("st_cep_cond")] public string? ZipCode { get; set; }
        public string? ExternalIdValue => ExternalId.ValueKind is JsonValueKind.String ? ExternalId.GetString() : ExternalId.ValueKind is JsonValueKind.Number ? ExternalId.GetRawText() : null;
        public SuperlogicaCondominium ToModel() => new(ExternalIdValue!.Trim(), Clean(Name) ?? Clean(TradeName) ?? ExternalIdValue!.Trim(), Clean(TradeName), RegistrationData.Digits(TaxId), Clean(Address), Clean(AddressComplement), Clean(Neighborhood), Clean(City), Clean(State)?.ToUpperInvariant(), RegistrationData.Digits(ZipCode));
        private static string? Clean(string? value) => RegistrationData.Optional(value);
    }
}

public static class SuperlogicaIntegrationEndpoints
{
    private const string Provider = "Superlogica";
    public static IEndpointRouteBuilder MapSuperlogicaIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/overwatch/management-companies/{administratorId:guid}/integrations/superlogica")
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        group.MapGet("", GetAsync);
        group.MapPut("", ConfigureAsync);
        group.MapPost("/validate", ValidateAsync);
        group.MapDelete("", DeleteAsync);
        group.MapGet("/condominiums", ListCondominiumsAsync);
        group.MapPost("/mappings", LinkExistingAsync);
        group.MapPost("/condominiums/create", CreateAndLinkAsync);
        group.MapDelete("/mappings/{mappingId:guid}", UnlinkAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(Guid administratorId, AppDbContext db, CancellationToken ct)
    {
        if (!await db.ManagementCompanies.AnyAsync(x => x.Id == administratorId, ct)) return Results.NotFound();
        var item = await db.AdministratorIntegrations.AsNoTracking().SingleOrDefaultAsync(x => x.AdministratorId == administratorId && x.Provider == Provider, ct);
        return Results.Ok(ToResponse(item));
    }

    private static async Task<IResult> ConfigureAsync(Guid administratorId, CredentialsInput input, AppDbContext db, IDataProtectionProvider protection, ISuperlogicaClient client, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.AppToken) || string.IsNullOrWhiteSpace(input.AccessToken) || string.IsNullOrWhiteSpace(input.Secret)) return Results.BadRequest(new { message = "Informe as três credenciais da Superlógica." });
        if (!await db.ManagementCompanies.AnyAsync(x => x.Id == administratorId, ct)) return Results.NotFound();
        var protector = protection.CreateProtector("Comvy.AdministratorIntegration.Superlogica.v1");
        var item = await db.AdministratorIntegrations.SingleOrDefaultAsync(x => x.AdministratorId == administratorId && x.Provider == Provider, ct);
        var app = protector.Protect(input.AppToken); var access = protector.Protect(input.AccessToken); var secret = protector.Protect(input.Secret);
        if (item is null) { item = new AdministratorIntegration(administratorId, Provider, app, access, secret); db.Add(item); }
        else item.SetCredentials(app, access, secret);
        item.SetValidation("NotConfigured");
        await db.SaveChangesAsync(ct);
        return await ValidateAndRespondAsync(item, db, protection, client, ct);
    }

    private static async Task<IResult> ValidateAsync(Guid administratorId, AppDbContext db, IDataProtectionProvider protection, ISuperlogicaClient client, CancellationToken ct)
    {
        var item = await db.AdministratorIntegrations.SingleOrDefaultAsync(x => x.AdministratorId == administratorId && x.Provider == Provider, ct);
        if (item is null) return Results.NotFound(new { message = "Integração não configurada." });
        return await ValidateAndRespondAsync(item, db, protection, client, ct);
    }

    private static async Task<IResult> ValidateAndRespondAsync(AdministratorIntegration item, AppDbContext db, IDataProtectionProvider protection, ISuperlogicaClient client, CancellationToken ct)
    {
        try
        {
            var protector = protection.CreateProtector("Comvy.AdministratorIntegration.Superlogica.v1");
            var result = await client.ValidateAsync(protector.Unprotect(item.EncryptedAppToken), protector.Unprotect(item.EncryptedAccessToken), ct);
            item.SetValidation(result.Success ? "Connected" : result.InvalidCredentials ? "Invalid" : "ValidationFailed", result.Success ? DateTime.UtcNow : null);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { success = result.Success, message = result.Success ? "Conexão validada." : result.InvalidCredentials ? "As credenciais informadas não foram aceitas pela Superlógica." : "A Superlógica não concluiu a validação. Tente novamente mais tarde.", integration = ToResponse(item) });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            item.SetValidation("ValidationFailed"); await db.SaveChangesAsync(ct);
            return Results.Ok(new { success = false, message = "A Superlógica demorou para responder. Tente novamente mais tarde.", integration = ToResponse(item) });
        }
        catch (HttpRequestException)
        {
            item.SetValidation("ValidationFailed"); await db.SaveChangesAsync(ct);
            return Results.Ok(new { success = false, message = "Não foi possível acessar a Superlógica. Tente novamente mais tarde.", integration = ToResponse(item) });
        }
    }

    private static async Task<IResult> DeleteAsync(Guid administratorId, AppDbContext db, CancellationToken ct)
    {
        var item = await db.AdministratorIntegrations.SingleOrDefaultAsync(x => x.AdministratorId == administratorId && x.Provider == Provider, ct);
        if (item is not null) { db.Remove(item); await db.SaveChangesAsync(ct); }
        return Results.NoContent();
    }

    private static async Task<IResult> ListCondominiumsAsync(Guid administratorId, AppDbContext db, IDataProtectionProvider protection, ISuperlogicaClient client, CancellationToken ct)
    {
        var integration = await GetIntegrationAsync(administratorId, db, ct);
        if (integration is null) return Results.NotFound(new { message = "Integração não configurada." });
        try
        {
            var protector = protection.CreateProtector("Comvy.AdministratorIntegration.Superlogica.v1");
            var result = await client.ListCondominiumsAsync(protector.Unprotect(integration.EncryptedAppToken), protector.Unprotect(integration.EncryptedAccessToken), ct);
            if (result.InvalidCredentials) return Results.BadRequest(new { code = "invalid_credentials", message = "Credenciais inválidas. Revise a integração da Superlógica." });
            if (result.Condominiums is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            var mappings = await db.ExternalCondominiumMappings.AsNoTracking().Where(x => x.AdministratorIntegrationId == integration.Id)
                .Select(x => new { x.ExternalCondominiumId, x.CondominiumId, CondominiumName = x.Condominium.Name, x.Id }).ToListAsync(ct);
            var candidates = await db.Condominiums.AsNoTracking().Where(x => x.ManagementCompanyId == administratorId &&
                    !db.ExternalCondominiumMappings.Any(mapping => mapping.AdministratorIntegrationId == integration.Id && mapping.CondominiumId == x.Id))
                .Select(x => new { x.Id, x.Name, x.Cnpj }).ToListAsync(ct);
            var records = result.Condominiums.Select(external =>
            {
                var mapping = mappings.SingleOrDefault(x => x.ExternalCondominiumId == external.ExternalId);
                var matches = external.TaxId is null ? [] : candidates.Where(x => RegistrationData.Digits(x.Cnpj) == external.TaxId).ToArray();
                return new { condominium = external, mapping = mapping is null ? null : new { id = mapping.Id, condominiumId = mapping.CondominiumId, name = mapping.CondominiumName }, cnpjCandidate = mapping is null && matches.Length == 1 ? new { id = matches[0].Id, name = matches[0].Name } : null };
            }).ToArray();
            return Results.Ok(new { administratorName = await db.ManagementCompanies.Where(x => x.Id == administratorId).Select(x => x.Name).SingleAsync(ct), total = records.Length, availableCondominiums = candidates, items = records });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        catch (HttpRequestException) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        catch (JsonException) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        catch (System.Security.Cryptography.CryptographicException) { return Results.BadRequest(new { code = "invalid_credentials", message = "Revise as credenciais da integração Superlógica." }); }
    }

    private static async Task<IResult> LinkExistingAsync(Guid administratorId, MappingInput input, AppDbContext db, CancellationToken ct)
    {
        var integration = await GetIntegrationAsync(administratorId, db, ct);
        if (integration is null) return Results.NotFound(new { message = "Integração não configurada." });
        if (string.IsNullOrWhiteSpace(input.ExternalCondominiumId) || input.ExternalCondominiumId.Trim().Length > 100) return Results.BadRequest(new { message = "ID externo inválido." });
        var condominium = await db.Condominiums.SingleOrDefaultAsync(x => x.Id == input.CondominiumId, ct);
        if (condominium is null) return Results.NotFound(new { message = "Condomínio não encontrado." });
        if (condominium.ManagementCompanyId != administratorId) return Results.Conflict(new { message = "O condomínio não pertence atualmente a esta administradora." });
        var mapping = new ExternalCondominiumMapping(integration.Id, condominium.Id, input.ExternalCondominiumId);
        db.ExternalCondominiumMappings.Add(mapping);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "Condomínio externo ou condomínio Comvy já vinculado nesta integração." }); }
        return Results.Created($"/overwatch/management-companies/{administratorId}/integrations/superlogica/mappings/{mapping.Id}", new { mapping.Id });
    }

    private static async Task<IResult> CreateAndLinkAsync(Guid administratorId, CreateMappingInput input, AppDbContext db, CancellationToken ct)
    {
        var integration = await GetIntegrationAsync(administratorId, db, ct);
        if (integration is null) return Results.NotFound(new { message = "Integração não configurada." });
        var request = new CondominiumRequest(input.Name, null, input.Cnpj, input.Address, input.City, input.State, false, false, null, true);
        var result = await OverwatchCondominiumCreationService.CreateAsync(db, request, administratorId, integration.Id, input.ExternalCondominiumId, ct);
        if (result.Error is not null)
            return result.Conflict ? Results.Conflict(new { message = result.Error }) : Results.BadRequest(new { message = result.Error });
        return Results.Created($"/overwatch/condominiums/{result.Condominium!.Id}", new { condominiumId = result.Condominium.Id, mappingId = result.Mapping!.Id });
    }

    private static async Task<IResult> UnlinkAsync(Guid administratorId, Guid mappingId, AppDbContext db, CancellationToken ct)
    {
        var integration = await GetIntegrationAsync(administratorId, db, ct);
        if (integration is null) return Results.NotFound();
        var mapping = await db.ExternalCondominiumMappings.SingleOrDefaultAsync(x => x.Id == mappingId && x.AdministratorIntegrationId == integration.Id, ct);
        if (mapping is null) return Results.NotFound();
        db.ExternalCondominiumMappings.Remove(mapping);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static Task<AdministratorIntegration?> GetIntegrationAsync(Guid administratorId, AppDbContext db, CancellationToken ct) =>
        db.AdministratorIntegrations.SingleOrDefaultAsync(x => x.AdministratorId == administratorId && x.Provider == Provider, ct);

    public sealed record MappingInput(string ExternalCondominiumId, Guid CondominiumId);
    public sealed record CreateMappingInput(string ExternalCondominiumId, string? Name, string? Cnpj, string? Address, string? City, string? State);

    private static object ToResponse(AdministratorIntegration? item) => new { provider = Provider, configured = item is not null, status = item?.Status ?? "NotConfigured", lastValidatedAt = item?.LastValidatedAt };
    public sealed record CredentialsInput(string AppToken, string AccessToken, string Secret);
}
