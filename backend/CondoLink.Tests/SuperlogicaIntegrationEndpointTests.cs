using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Overwatch.ManagementCompanies;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CondoLink.Tests;

public sealed class SuperlogicaIntegrationEndpointTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private WebApplication? _application;
    private HttpClient _admin = null!;
    private Guid _administratorId;
    private readonly FakeSuperlogicaClient _provider = new();

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton<ISuperlogicaClient>(_provider);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.TestScheme;
                options.DefaultChallengeScheme = TestAuthHandler.TestScheme;
                options.DefaultForbidScheme = TestAuthHandler.TestScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.TestScheme, _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy(DependencyInjection.PlatformAdminPolicy,
            policy => policy.RequireRole(DependencyInjection.PlatformAdminRole)));
        _application = builder.Build();
        _application.UseAuthentication(); _application.UseAuthorization();
        _application.MapSuperlogicaIntegrationEndpoints();
        await _application.StartAsync();
        _admin = _application.GetTestClient();
        _admin.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        await using var scope = _application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var company = new ManagementCompany("Administradora", null, null, "admin@example.com", null);
        db.ManagementCompanies.Add(company);
        await db.SaveChangesAsync();
        _administratorId = company.Id;
    }

    public async Task DisposeAsync()
    {
        _admin.Dispose();
        if (_application is not null) { await _application.StopAsync(); await _application.DisposeAsync(); }
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Saves_encrypted_credentials_validates_and_never_returns_tokens()
    {
        var sentHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _provider.ValidationClient = new SuperlogicaClient(new HttpClient(new StubHttpHandler(request =>
        {
            AssertCondominiumsRequest(request);
            sentHeaders["app_token"] = request.Headers.GetValues("app_token").Single();
            sentHeaders["access_token"] = request.Headers.GetValues("access_token").Single();
            return new HttpResponseMessage(HttpStatusCode.OK);
        })) { BaseAddress = new Uri("https://api.superlogica.net") });
        const string token = "TOKEN_TESTE_123";
        const string accessToken = "ACCESS_TESTE_456";
        const string secret = "SECRET_TESTE_789";
        var response = await _admin.PutAsJsonAsync(Path, new { appToken = token, accessToken, secret });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(token, body); Assert.DoesNotContain(accessToken, body); Assert.DoesNotContain(secret, body);
        Assert.Contains("Connected", body);
        Assert.Equal(token, _provider.AppToken); Assert.Equal(accessToken, _provider.AccessToken);
        Assert.Equal(token, sentHeaders["app_token"]);
        Assert.Equal(accessToken, sentHeaders["access_token"]);
        Assert.DoesNotContain(secret, sentHeaders.Values);
        await using var scope = _application!.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var stored = await services.GetRequiredService<AppDbContext>().AdministratorIntegrations.SingleAsync();
        Assert.DoesNotContain(token, stored.EncryptedAppToken);
        Assert.DoesNotContain(accessToken, stored.EncryptedAccessToken);
        Assert.DoesNotContain(secret, stored.EncryptedSecret);
        var protector = services.GetRequiredService<IDataProtectionProvider>().CreateProtector("Comvy.AdministratorIntegration.Superlogica.v1");
        Assert.Equal(token, protector.Unprotect(stored.EncryptedAppToken));
        Assert.Equal(accessToken, protector.Unprotect(stored.EncryptedAccessToken));
        Assert.Equal(secret, protector.Unprotect(stored.EncryptedSecret));
        var updated = await _admin.PutAsJsonAsync(Path, new { appToken = "APP_UPDATED", accessToken = "ACCESS_UPDATED", secret = "SECRET_UPDATED" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.DoesNotContain("APP_UPDATED", await updated.Content.ReadAsStringAsync());
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().AdministratorIntegrations.CountAsync());
    }

    [Fact]
    public async Task Missing_integration_returns_not_found_and_delete_removes_credentials()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.PostAsync($"{Path}/validate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.GetAsync($"{Path}/condominiums")).StatusCode);
        await _admin.PutAsJsonAsync(Path, new { appToken = "a", accessToken = "b", secret = "c" });
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.DeleteAsync(Path)).StatusCode);
        var state = await _admin.GetFromJsonAsync<IntegrationState>(Path);
        Assert.False(state!.Configured);
    }

    [Fact]
    public async Task Invalid_credentials_mark_invalid_and_common_user_is_forbidden()
    {
        _provider.Result = new(false, true);
        var response = await _admin.PutAsJsonAsync(Path, new { appToken = "a", accessToken = "b", secret = "c" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid", await response.Content.ReadAsStringAsync());
        using var common = _application!.GetTestClient(); common.DefaultRequestHeaders.Add("X-Test-Role", "Resident");
        Assert.Equal(HttpStatusCode.Forbidden, (await common.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await common.GetAsync($"{Path}/condominiums")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.GetAsync($"{Path}/condominiums")).StatusCode);
    }

    [Fact]
    public async Task Timeout_keeps_configuration_and_marks_temporary_failure()
    {
        _provider.ThrowTimeout = true;
        var response = await _admin.PutAsJsonAsync(Path, new { appToken = "a", accessToken = "b", secret = "c" });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ValidationFailed", body); Assert.Contains("demorou", body);
        Assert.DoesNotContain("\"appToken\"", body); Assert.DoesNotContain("\"accessToken\"", body);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await _admin.GetAsync($"{Path}/condominiums")).StatusCode);
    }

    [Fact]
    public async Task Discovery_returns_normalized_rows_and_cnpj_candidate_without_mapping()
    {
        await _admin.PutAsJsonAsync(Path, new { appToken = "app", accessToken = "access", secret = "secret" });
        var cnpj = "11222333000181";
        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var condominium = new Condominium("Residencial Monticello", null, cnpj, "Rua A", "Maringá", "PR", false, false, null);
            condominium.SetManagementCompany(_administratorId);
            db.Condominiums.Add(condominium);
            await db.SaveChangesAsync();
        }
        _provider.Condominiums = [new("28", " Residencial Monticello ", null, "11.222.333/0001-81", " Rua A ", null, null, "Maringá", "pr", "87000-000")];
        var response = await _admin.GetAsync($"{Path}/condominiums");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("cnpjCandidate", body);
        Assert.Contains("\"externalId\":\"28\"", body);
        Assert.DoesNotContain("appToken", body);
        await using var verify = _application!.Services.CreateAsyncScope();
        Assert.Empty(await verify.ServiceProvider.GetRequiredService<AppDbContext>().ExternalCondominiumMappings.ToListAsync());
    }

    [Fact]
    public async Task Existing_link_requires_current_administrator_and_duplicate_mappings_conflict()
    {
        await _admin.PutAsJsonAsync(Path, new { appToken = "app", accessToken = "access", secret = "secret" });
        Guid condominiumId; Guid otherCondominiumId; Guid secondCondominiumId;
        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var condominium = new Condominium("Vinculado", null, null, null, null, null, false, false, null);
            condominium.SetManagementCompany(_administratorId); db.Condominiums.Add(condominium);
            var otherCompany = new ManagementCompany("Outra", null, null, "outra@example.com", null);
            db.ManagementCompanies.Add(otherCompany);
            var otherCondominium = new Condominium("De outra administradora", null, null, null, null, null, false, false, null);
            otherCondominium.SetManagementCompany(otherCompany.Id); db.Condominiums.Add(otherCondominium);
            var secondCondominium = new Condominium("Outro condomínio", null, null, null, null, null, false, false, null);
            secondCondominium.SetManagementCompany(_administratorId); db.Condominiums.Add(secondCondominium);
            await db.SaveChangesAsync();
            condominiumId = condominium.Id;
            otherCondominiumId = otherCondominium.Id; secondCondominiumId = secondCondominium.Id;
            _provider.OtherAdministratorId = otherCompany.Id;
        }
        var linked = await _admin.PostAsJsonAsync($"{Path}/mappings", new { externalCondominiumId = "28", condominiumId });
        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.PostAsJsonAsync($"{Path}/mappings", new { externalCondominiumId = "29", condominiumId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.PostAsJsonAsync($"{Path}/mappings", new { externalCondominiumId = "28", condominiumId = secondCondominiumId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.PostAsJsonAsync($"{Path}/mappings", new { externalCondominiumId = "29", condominiumId = otherCondominiumId })).StatusCode);
        var mappingId = System.Text.Json.JsonDocument.Parse(await linked.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.DeleteAsync($"{Path}/mappings/{mappingId}")).StatusCode);
        await using var verify = _application!.Services.CreateAsyncScope();
        Assert.Equal("Vinculado", (await verify.ServiceProvider.GetRequiredService<AppDbContext>().Condominiums.SingleAsync(x => x.Id == condominiumId)).Name);
    }

    [Fact]
    public async Task Creation_is_atomic_and_creates_administrator_link_and_mapping()
    {
        await _admin.PutAsJsonAsync(Path, new { appToken = "app", accessToken = "access", secret = "secret" });
        await using (var scope = _application!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var integration = await db.AdministratorIntegrations.SingleAsync();
            var existing = new Condominium("Condomínio existente", null, null, null, null, null, false, false, null);
            existing.SetManagementCompany(_administratorId);
            db.Condominiums.Add(existing);
            db.ExternalCondominiumMappings.Add(new ExternalCondominiumMapping(integration.Id, existing.Id, "28"));
            await db.SaveChangesAsync();
        }
        var request = new { externalCondominiumId = "28", name = "Novo condomínio", cnpj = "11.222.333/0001-81", address = "Rua A", city = "Maringá", state = "PR" };
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.PostAsJsonAsync($"{Path}/condominiums/create", request)).StatusCode);
        await using (var verify = _application!.Services.CreateAsyncScope())
            Assert.False(await verify.ServiceProvider.GetRequiredService<AppDbContext>().Condominiums.AnyAsync(x => x.Name == "Novo condomínio"));

        var created = await _admin.PostAsJsonAsync($"{Path}/condominiums/create", request with { externalCondominiumId = "29" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await using var finalScope = _application!.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var condominium = await finalDb.Condominiums.SingleAsync(x => x.Name == "Novo condomínio");
        Assert.Equal(_administratorId, condominium.ManagementCompanyId);
        Assert.True(await finalDb.CondominiumManagementCompanyLinks.AnyAsync(x => x.CondominiumId == condominium.Id && x.IsActive));
        Assert.True(await finalDb.ExternalCondominiumMappings.AnyAsync(x => x.CondominiumId == condominium.Id && x.ExternalCondominiumId == "29"));
    }

    [Fact]
    public async Task Http_client_maps_superlogica_fields_to_normalized_model()
    {
        var sentHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var handler = new StubHttpHandler(request =>
        {
            AssertCondominiumsRequest(request);
            sentHeaders["app_token"] = request.Headers.GetValues("app_token").Single();
            sentHeaders["access_token"] = request.Headers.GetValues("access_token").Single();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"id_condominio_cond":28,"st_nome_cond":" Residencial X ","st_fantasia_cond":" X ","st_cpf_cond":"11.222.333/0001-81","st_endereco_cond":" Rua A ","st_complemento_cond":" Bloco B ","st_bairro_cond":" Centro ","st_cidade_cond":" Maringá ","st_uf_uf":"pr","st_cep_cond":"87.000-000"}]""")
            };
        });
        var result = await new SuperlogicaClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.superlogica.net") })
            .ListCondominiumsAsync("TOKEN_TESTE_123", "ACCESS_TESTE_456", CancellationToken.None);
        Assert.Equal("TOKEN_TESTE_123", sentHeaders["app_token"]);
        Assert.Equal("ACCESS_TESTE_456", sentHeaders["access_token"]);
        Assert.DoesNotContain("SECRET_TESTE_789", sentHeaders.Values);
        var item = Assert.Single(result.Condominiums!);
        Assert.Equal("28", item.ExternalId);
        Assert.Equal("Residencial X", item.Name);
        Assert.Equal("X", item.TradeName);
        Assert.Equal("11222333000181", item.TaxId);
        Assert.Equal("87000000", item.ZipCode);
        Assert.Equal("PR", item.State);
    }

    [Fact]
    public async Task Validate_endpoint_does_not_report_not_found_as_invalid_credentials()
    {
        _provider.ValidationClient = new SuperlogicaClient(new HttpClient(new StubHttpHandler(request =>
        {
            AssertCondominiumsRequest(request);
            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("route not found") };
        })) { BaseAddress = new Uri("https://api.superlogica.net") });

        var response = await _admin.PutAsJsonAsync(Path, new
        {
            appToken = "TOKEN_TESTE_123", accessToken = "ACCESS_TESTE_456", secret = "SECRET_TESTE_789"
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ValidationFailed", body);
        Assert.Contains("não concluiu a validação", body);
        Assert.DoesNotContain("não foram aceitas", body);
    }

    private static void AssertCondominiumsRequest(HttpRequestMessage request)
    {
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.superlogica.net/v2/condor/condominios/get", request.RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Equal("?id=-1&somenteCondominiosAtivos=1&ignorarCondominioModelo=1&apenasColunasPrincipais=1&apenasDadosDoPlanoDeContas=0&comDataFechamento=1&itensPorPagina=50&pagina=1", request.RequestUri.Query);
        Assert.Equal("application/json", request.Content?.Headers.ContentType?.MediaType);
        Assert.Null(request.Headers.Authorization);
    }

    private string Path => $"/overwatch/management-companies/{_administratorId}/integrations/superlogica";

    private sealed record IntegrationState(string Provider, bool Configured, string Status, DateTime? LastValidatedAt);

    private sealed class FakeSuperlogicaClient : ISuperlogicaClient
    {
        public string? AppToken { get; private set; }
        public string? AccessToken { get; private set; }
        public ISuperlogicaClient? ValidationClient { get; set; }
        public SuperlogicaValidationResult Result { get; set; } = new(true, false);
        public bool ThrowTimeout { get; set; }
        public Guid OtherAdministratorId { get; set; }
        public IReadOnlyList<SuperlogicaCondominium> Condominiums { get; set; } = [];
        public Task<SuperlogicaValidationResult> ValidateAsync(string appToken, string accessToken, CancellationToken cancellationToken)
        {
            AppToken = appToken; AccessToken = accessToken;
            if (ThrowTimeout) throw new OperationCanceledException();
            if (ValidationClient is not null) return ValidationClient.ValidateAsync(appToken, accessToken, cancellationToken);
            return Task.FromResult(Result);
        }
        public Task<SuperlogicaCondominiumListResult> ListCondominiumsAsync(string appToken, string accessToken, CancellationToken cancellationToken)
        {
            AppToken = appToken; AccessToken = accessToken;
            if (ThrowTimeout) throw new HttpRequestException("provider unavailable");
            return Task.FromResult(Result.InvalidCredentials
                ? new SuperlogicaCondominiumListResult(null, true)
                : new SuperlogicaCondominiumListResult(Condominiums, false));
        }
        public Task<SuperlogicaUnitPageResult> ListUnitsPageAsync(string appToken, string accessToken, string externalCondominiumId, int page, CancellationToken cancellationToken) =>
            Task.FromResult(new SuperlogicaUnitPageResult([], false));
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
