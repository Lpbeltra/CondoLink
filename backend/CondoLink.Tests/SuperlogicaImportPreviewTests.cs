using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Overwatch.ManagementCompanies;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CondoLink.Tests;

public sealed class SuperlogicaImportPreviewTests
{
    [Fact]
    public async Task Preview_requires_platform_admin_and_mapping()
    {
        await using var host = await Harness.StartAsync(withMapping: false, _ => Json(HttpStatusCode.OK, "[]"));
        using var common = host.App.GetTestClient(); common.DefaultRequestHeaders.Add("X-Test-Role", "Resident");
        Assert.Equal(HttpStatusCode.Forbidden, (await common.GetAsync(host.Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Admin.GetAsync(host.Path)).StatusCode);
        Assert.Empty(host.Handler.Requests);
    }

    [Fact]
    public async Task Preview_uses_mapping_id_paginates_groups_rows_and_matches_comvy_without_writes()
    {
        var pages = new Dictionary<int, string>
        {
            [1] = PageOne(),
            [2] = """[{"id_unidade_uni":5159,"st_unidade_uni":"0107","st_bloco_uni":" 01 ","id_proprietario":88,"nome_proprietario":"Pessoa A","email_proprietario":" PESSOA@example.com ","telefone_proprietario":"+55 44 999606150","cpf_proprietario":"123.456.789-00","tipo_proprietario":"1"}]"""
        };
        await using var host = await Harness.StartAsync(withMapping: true, request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("APP_PRIVATE", request.Headers.GetValues("app_token").Single());
            Assert.Equal("ACCESS_PRIVATE", request.Headers.GetValues("access_token").Single());
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            Assert.Equal("28", query["idCondominio"]);
            Assert.Equal("1", query["exibirDadosDosContatos"]);
            Assert.Equal("0", query["exibirGruposDasUnidades"]);
            Assert.Equal("0", query["exibirInadimplencia"]);
            Assert.Equal("50", query["itensPorPagina"]);
            var page = int.Parse(query["pagina"]!);
            return pages.TryGetValue(page, out var content) ? Json(HttpStatusCode.OK, content) : Json(HttpStatusCode.OK, "[]");
        });

        var response = await host.Admin.GetAsync(host.Path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("APP_PRIVATE", body); Assert.DoesNotContain("ACCESS_PRIVATE", body);
        Assert.DoesNotContain("12345678900", body);
        Assert.Equal(2, host.Handler.Requests.Count);
        using var json = System.Text.Json.JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal("28", root.GetProperty("externalCondominiumId").GetString());
        Assert.Equal(51, root.GetProperty("summary").GetProperty("externalRecordsRead").GetInt32());
        Assert.Equal(49, root.GetProperty("summary").GetProperty("units").GetInt32());
        var block = Assert.Single(root.GetProperty("blocks").EnumerateArray());
        Assert.Equal("01", block.GetProperty("identifier").GetString());
        var existingUnit = Assert.Single(block.GetProperty("units").EnumerateArray());
        Assert.Equal("5159", existingUnit.GetProperty("externalUnitId").GetString());
        Assert.Equal("Existing", existingUnit.GetProperty("status").GetString());
        var contacts = existingUnit.GetProperty("contacts").EnumerateArray().ToArray();
        Assert.Equal(2, contacts.Length);
        var matchedContact = Assert.Single(contacts, x => x.GetProperty("externalIds").GetArrayLength() == 2);
        Assert.Equal("Existing", matchedContact.GetProperty("personStatus").GetString());
        Assert.Equal("NoChange", matchedContact.GetProperty("unitRelationshipStatus").GetString());
        Assert.Equal("123.456.789-00".Length, matchedContact.GetProperty("taxIdMasked").GetString()!.Length);
        Assert.Equal(new[] { "1", "2" }, matchedContact.GetProperty("relationships").EnumerateArray()
            .Select(x => x.GetProperty("externalRelationshipType").GetString()).Order().ToArray());
        Assert.Contains(contacts, x => x.GetProperty("name").GetString() == "Pessoa B"
            && x.GetProperty("personStatus").GetString() == "New");
        Assert.Equal(48, root.GetProperty("unitsWithoutBlock").GetArrayLength());

        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.CondominiumBlocks.CountAsync()); Assert.Equal(1, await db.Units.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync()); Assert.Equal(1, await db.UnitMemberships.CountAsync());
    }

    [Fact]
    public async Task Empty_page_ends_and_second_page_failure_discards_partial_preview()
    {
        await using (var host = await Harness.StartAsync(true, request =>
        {
            var page = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["pagina"];
            return page == "1" ? Json(HttpStatusCode.OK, PageOne()) : Json(HttpStatusCode.OK, "[]");
        }))
        {
            Assert.Equal(HttpStatusCode.OK, (await host.Admin.GetAsync(host.Path)).StatusCode);
            Assert.Equal(2, host.Handler.Requests.Count);
        }
        await using (var host = await Harness.StartAsync(true, request =>
        {
            var page = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["pagina"];
            return page == "1" ? Json(HttpStatusCode.OK, PageOne()) : Json(HttpStatusCode.ServiceUnavailable, "technical secret body");
        }))
        {
            var response = await host.Admin.GetAsync(host.Path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.DoesNotContain("technical secret", await response.Content.ReadAsStringAsync());
            await using var scope = host.App.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.ExternalCondominiumMappings.CountAsync());
            Assert.Equal(1, await db.Units.CountAsync());
        }
    }

    [Fact]
    public async Task Missing_mapping_fails_without_call_or_database_changes()
    {
        await using var host = await Harness.StartAsync(false, _ => Json(HttpStatusCode.OK, "[]"));
        var before = await host.CountsAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await host.Admin.GetAsync(host.Path)).StatusCode);
        Assert.Equal(before, await host.CountsAsync());
        Assert.Empty(host.Handler.Requests);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
    };

    private static string PageOne()
    {
        var rows = new List<string>
        {
            """{"id_unidade_uni":5159,"st_unidade_uni":" 0107 ","st_bloco_uni":"01","id_contato_con":7694,"st_nome_con":"Pessoa A","st_email_con":" PESSOA@example.com ","st_telefone_con":"+55 44 999606150;+55 44 999317788","st_fax_con":"inválido","st_cpf_con":"123.456.789-00","id_tiporesp_tres":2,"dt_entrada_res":" 2022-01-01 "}""",
            """{"id_unidade_uni":5159,"st_unidade_uni":"0107","st_bloco_uni":"01","id_contato_con":10532,"st_nome_con":"Pessoa B","st_email_con":"pessoa.b@example.com","st_telefone_con":"+55 44 999317788","id_tiporesp_tres":7}"""
        };
        for (var index = 0; index < 48; index++)
            rows.Add($"{{\"id_unidade_uni\":{6000 + index},\"st_unidade_uni\":\"{100 + index}\"}}");
        return $"[{string.Join(',', rows)}]";
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private Harness() { }
        public WebApplication App { get; private set; } = null!;
        public HttpClient Admin { get; private set; } = null!;
        public RecordingHandler Handler { get; private set; } = null!;
        public Guid CondominiumId { get; private set; }
        public string Path => $"/overwatch/condominiums/{CondominiumId}/integrations/superlogica/import-preview";

        public static async Task<Harness> StartAsync(bool withMapping, Func<HttpRequestMessage, HttpResponseMessage> response)
        {
            var host = new Harness(); await host._connection.OpenAsync();
            host.Handler = new RecordingHandler(response);
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
            builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(host._connection));
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddSingleton<ISuperlogicaClient>(new SuperlogicaClient(new HttpClient(host.Handler) { BaseAddress = new Uri("https://mock.superlogica.test") }));
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.TestScheme;
                options.DefaultChallengeScheme = TestAuthHandler.TestScheme;
                options.DefaultForbidScheme = TestAuthHandler.TestScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.TestScheme, _ => { });
            builder.Services.AddAuthorization(options => options.AddPolicy(DependencyInjection.PlatformAdminPolicy,
                policy => policy.RequireRole(DependencyInjection.PlatformAdminRole)));
            host.App = builder.Build(); host.App.UseAuthentication(); host.App.UseAuthorization(); host.App.MapSuperlogicaImportPreviewEndpoints();
            await host.App.StartAsync(); host.Admin = host.App.GetTestClient(); host.Admin.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
            await using var scope = host.App.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.EnsureCreatedAsync();
            var company = new ManagementCompany("Dimarp", null, null, "dimarp@example.com", null); db.ManagementCompanies.Add(company);
            var condominium = new Condominium("Monticello", null, null, null, null, null, false, false, null); condominium.SetManagementCompany(company.Id); db.Condominiums.Add(condominium); host.CondominiumId = condominium.Id;
            var integration = new AdministratorIntegration(company.Id, "Superlogica", "", "", "");
            var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Comvy.AdministratorIntegration.Superlogica.v1");
            integration.SetCredentials(protector.Protect("APP_PRIVATE"), protector.Protect("ACCESS_PRIVATE"), protector.Protect("SECRET_PRIVATE")); integration.SetValidation("Connected"); db.AdministratorIntegrations.Add(integration);
            if (withMapping) db.ExternalCondominiumMappings.Add(new ExternalCondominiumMapping(integration.Id, condominium.Id, "28"));
            var block = new CondominiumBlock(condominium.Id, "01"); db.CondominiumBlocks.Add(block);
            var unit = new Unit(condominium.Id, "0107", block.Id, null, null); db.Units.Add(unit);
            var user = new ApplicationUser("Pessoa A", "pessoa@example.com", "+55 44 999606150"); user.NormalizedUserName = user.UserName!.ToUpperInvariant(); user.NormalizedEmail = user.Email!.ToUpperInvariant(); user.UpdateManagerProfile("Pessoa A", "+55 44 999606150", "123.456.789-00", null, null, null, null); db.Users.Add(user);
            var membership = new CondominiumMembership(user.Id, condominium.Id); db.CondominiumMemberships.Add(membership);
            db.CondominiumMembershipRoles.Add(new CondominiumMembershipRole(membership.Id, CondominiumRole.Resident));
            db.UnitMemberships.Add(new UnitMembership(user.Id, unit.Id, UnitRelationshipType.Owner, true, true));
            await db.SaveChangesAsync();
            return host;
        }

        public async Task<(int Blocks, int Units, int Users, int CondoMemberships, int UnitMemberships)> CountsAsync()
        {
            await using var scope = App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return (await db.CondominiumBlocks.CountAsync(), await db.Units.CountAsync(), await db.Users.CountAsync(), await db.CondominiumMemberships.CountAsync(), await db.UnitMemberships.CountAsync());
        }

        public async ValueTask DisposeAsync()
        {
            Admin.Dispose(); await App.StopAsync(); await App.DisposeAsync(); await _connection.DisposeAsync();
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Requests.Add(request); return Task.FromResult(response(request)); }
    }
}
