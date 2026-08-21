using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using CondoLink.Api.Features.CondominiumSetup;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class CondominiumSetupEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _condominiumId;
    private Guid _otherCondominiumId;
    private Guid _managerId;
    private Guid _otherManagerId;
    private Guid _residentId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(
            application => application.MapCondominiumSetup());
        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Alfa", null, null);
            var other = new Condominium("Beta", null, null);
            var manager = CoreTestSeed.User(
                "Síndico Alfa", "setup-manager@example.com");
            var otherManager = CoreTestSeed.User(
                "Síndico Beta", "setup-other@example.com");
            var resident = CoreTestSeed.User(
                "Morador", "setup-resident@example.com");
            db.AddRange(
                condominium, other, manager, otherManager, resident);
            CoreTestSeed.AddMember(
                db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, otherManager.Id, other.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, resident.Id, condominium.Id, CondominiumRole.Resident);
            await db.SaveChangesAsync();
            _condominiumId = condominium.Id;
            _otherCondominiumId = other.Id;
            _managerId = manager.Id;
            _otherManagerId = otherManager.Id;
            _residentId = resident.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Csv_import_preserves_text_identifiers_and_only_previews()
    {
        using var form = new MultipartFormDataContent();
        form.Add(File(
            "Block,Unit,Floor,Description\r\nTower A,01,Ground,Store\r\n",
            "structure.csv"),
            "structureFile",
            "structure.csv");

        var response = await _host.ClientFor(_managerId).PostAsync(
            $"/condominiums/{_condominiumId}/setup/import/preview",
            form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content
            .ReadFromJsonAsync<SetupPreviewResponse>();
        Assert.Empty(preview!.Errors);
        Assert.Equal("01", Assert.Single(preview.Units).Unit);
        Assert.Equal(0, await _host.WithDbAsync(db => db.Units.CountAsync()));
    }

    [Fact]
    public async Task Xlsx_import_reads_inline_text_without_converting_leading_zeroes()
    {
        using var form = new MultipartFormDataContent();
        form.Add(
            new ByteArrayContent(CreateInlineStringXlsx(
            [
                ["Block", "Unit", "Floor", "Description"],
                ["", "001", "1", ""]
            ])),
            "structureFile",
            "structure.xlsx");

        var response = await _host.ClientFor(_managerId).PostAsync(
            $"/condominiums/{_condominiumId}/setup/import/preview",
            form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content
            .ReadFromJsonAsync<SetupPreviewResponse>();
        Assert.Equal("001", Assert.Single(preview!.Units).Unit);
    }

    [Fact]
    public async Task Generated_resident_template_is_accepted_by_its_own_parser()
    {
        var client = _host.ClientFor(_managerId);
        var template = await client.GetByteArrayAsync(
            $"/condominiums/{_condominiumId}/setup/templates/residents");
        using var form = new MultipartFormDataContent();
        form.Add(File("Block,Unit,Floor,Description\r\n,01,,\r\n", "structure.csv"),
            "structureFile", "structure.csv");
        form.Add(new ByteArrayContent(FillResidentTemplate(template,
            ["", "01", "Maria   Silva", " MARIA@example.com ", "(44) 99999-9999",
                "Proprietário", "Sim", "Sim"])), "residentsFile", "moradores.xlsx");

        var response = await client.PostAsync(
            $"/condominiums/{_condominiumId}/setup/import/preview", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<SetupPreviewResponse>();
        Assert.Empty(preview!.Errors);
        var resident = Assert.Single(preview.Residents);
        Assert.Equal("Maria Silva", resident.Name);
        Assert.Equal("maria@example.com", resident.Email);
        Assert.Equal("+5544999999999", resident.NormalizedPhone);
        Assert.Equal("Owner", resident.Relationship);
        Assert.Equal("01", resident.Unit);

        var confirmation = await client.PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/confirm", preview.Draft);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.Equal(1, await _host.WithDbAsync(db => db.UnitMemberships.CountAsync()));
    }

    [Theory]
    [InlineData("Proprietário", "(44) 99999-9999", "Owner", "+5544999999999")]
    [InlineData("Inquilino", "+5544999999999", "Tenant", "+5544999999999")]
    [InlineData("Morador autorizado", "+12125551234", "AuthorizedOccupant", "+12125551234")]
    public async Task Import_normalizes_relationship_phone_and_unambiguous_block_alias(
        string relationship, string phone, string expectedRelationship, string expectedPhone)
    {
        using var form = new MultipartFormDataContent();
        form.Add(File("Block,Unit,Floor,Description\r\nBloco 1,101A,,\r\n", "structure.csv"),
            "structureFile", "structure.csv");
        var residents = "Bloco,Unidade,Nome,E-mail,Telefone,Relacionamento,Morador,Residência principal\r\n"
            + $"1,101A,Maria Silva,maria-{expectedRelationship}@example.com,{phone},{relationship},Sim,Não\r\n";
        form.Add(File(residents, "moradores.csv"), "residentsFile", "moradores.csv");

        var response = await _host.ClientFor(_managerId).PostAsync(
            $"/condominiums/{_condominiumId}/setup/import/preview", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<SetupPreviewResponse>();
        Assert.Empty(preview!.Errors);
        var resident = Assert.Single(preview.Residents);
        Assert.Equal("Bloco 1", resident.Block);
        Assert.Equal(expectedRelationship, resident.Relationship);
        Assert.Equal(expectedPhone, resident.NormalizedPhone);
    }

    [Fact]
    public async Task Generator_supports_multiple_towers_and_segments()
    {
        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/generate/preview",
            new
            {
                towers = new[]
                {
                    Tower("Tower A", 1, 2, 2),
                    Tower("Tower B", 7, 7, 3)
                },
                residents = Array.Empty<object>()
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content
            .ReadFromJsonAsync<SetupPreviewResponse>();
        Assert.Empty(preview!.Errors);
        Assert.Equal(2, preview.Totals.Blocks);
        Assert.Equal(7, preview.Totals.Units);
        Assert.Contains(preview.Units, item =>
            item.Block == "Tower A" && item.Unit == "101");
        Assert.Contains(preview.Units, item =>
            item.Block == "Tower B" && item.Unit == "703");
    }

    [Fact]
    public async Task Condominium_without_units_can_be_confirmed()
    {
        var draft = new SetupRequest(true, [], []);
        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/confirm",
            draft);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await _host.WithDbAsync(db => db.Units.CountAsync()));
    }

    [Fact]
    public async Task Duplicate_validation_prevents_the_entire_batch_from_persisting()
    {
        var draft = new SetupRequest(
            false,
            [
                new SetupUnitRow(2, null, "01", null, null),
                new SetupUnitRow(3, null, "01", null, null),
                new SetupUnitRow(4, null, "02", null, null)
            ],
            []);

        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/confirm",
            draft);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _host.WithDbAsync(db => db.Units.CountAsync()));
    }

    [Fact]
    public async Task Confirmation_creates_user_and_all_links_atomically_without_exposing_credentials()
    {
        var draft = new SetupRequest(
            false,
            [new SetupUnitRow(2, "Tower A", "101", "1", null)],
            [
                new SetupResidentRow(
                    2,
                    "Tower A",
                    "101",
                    "Maria Silva",
                    "maria.setup@example.com",
                    "11999999999",
                    "Owner",
                    "Yes",
                    "Yes")
            ]);

        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/confirm",
            draft);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<SetupConfirmationResponse>();
        Assert.Empty(result!.Credentials);
        await _host.WithServicesAsync(async services =>
        {
            var userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(
                "maria.setup@example.com");
            Assert.NotNull(user);
            Assert.True(user!.MustChangePassword);
            Assert.True(user.ReceiveWhatsAppUpdates);
        });
        await _host.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.CondominiumBlocks.CountAsync());
            Assert.Equal(1, await db.Units.CountAsync());
            Assert.Equal(1, await db.UnitMemberships.CountAsync());
        });
    }

    [Fact]
    public async Task Existing_user_is_reused_without_generating_credentials()
    {
        await _host.WithServicesAsync(async services =>
        {
            var userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            var existing = new ApplicationUser(
                "Pessoa Existente",
                "existing.setup@example.com",
                null);
            Assert.True((await userManager.CreateAsync(
                existing, "Existing123")).Succeeded);
        });
        var draft = new SetupRequest(
            false,
            [],
            [
                new SetupResidentRow(
                    2,
                    null,
                    null,
                    "Pessoa Existente",
                    "existing.setup@example.com",
                    null,
                    null,
                    null,
                    null)
            ]);

        var previewResponse = await _host.ClientFor(_managerId)
            .PostAsJsonAsync(
                $"/condominiums/{_condominiumId}/setup/preview",
                draft);
        var preview = await previewResponse.Content
            .ReadFromJsonAsync<SetupPreviewResponse>();
        Assert.True(Assert.Single(preview!.Residents).ExistingUser);
        Assert.Equal(1, preview.Totals.ExistingUsers);

        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/confirm",
            draft);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<SetupConfirmationResponse>();
        Assert.Empty(result!.Credentials);
        Assert.Equal(1, await _host.WithDbAsync(db =>
            db.Users.CountAsync(item =>
                item.Email == "existing.setup@example.com")));
    }

    [Fact]
    public async Task Preview_accepts_explicit_international_phone_and_shows_e164()
    {
        var draft = new SetupRequest(false, [],
        [
            new SetupResidentRow(
                2, null, null, "John Smith", "john.setup@example.com",
                "+1 (212) 555-1234", null, null, null)
        ]);

        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/preview", draft);
        var preview = await response.Content
            .ReadFromJsonAsync<SetupPreviewResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(preview!.Errors);
        Assert.Equal("+12125551234", Assert.Single(preview.Residents).NormalizedPhone);
    }

    [Fact]
    public async Task Preview_blocks_email_and_phone_that_identify_different_users()
    {
        await _host.WithServicesAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await userManager.CreateAsync(
                new ApplicationUser("Email User", "email.identity@example.com", "+1 212 555 1000"),
                "Existing123")).Succeeded);
            Assert.True((await userManager.CreateAsync(
                new ApplicationUser("Phone User", "phone.identity@example.com", "+1 212 555 2000"),
                "Existing123")).Succeeded);
        });
        var draft = new SetupRequest(false, [],
        [
            new SetupResidentRow(
                2, null, null, "Imported User", "email.identity@example.com",
                "+1 212 555 2000", null, null, null)
        ]);

        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/preview", draft);
        var preview = await response.Content
            .ReadFromJsonAsync<SetupPreviewResponse>();

        Assert.Contains(preview!.Errors, issue =>
            issue.Line == 2 && issue.Reason.StartsWith("Conflict:"));
        Assert.Equal("Conflict", Assert.Single(preview.Residents).Status);
    }

    [Fact]
    public async Task Duplicate_resident_line_is_ignored_in_preview()
    {
        var row = new SetupResidentRow(
            2, null, null, "Maria", "duplicate.setup@example.com",
            "11999999999", null, null, null);
        var duplicate = row with { Line = 3 };

        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/preview",
            new SetupRequest(false, [], [row, duplicate]));
        var preview = await response.Content
            .ReadFromJsonAsync<SetupPreviewResponse>();

        Assert.Empty(preview!.Errors);
        Assert.Single(preview.Residents);
        Assert.Contains(preview.Warnings, issue =>
            issue.Line == 3 && issue.Reason.StartsWith("ExistingMembership:"));
    }

    [Fact]
    public async Task Persistence_failure_rolls_back_the_entire_batch()
    {
        await _host.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER fail_setup_membership
            BEFORE INSERT ON unit_memberships
            BEGIN
                SELECT RAISE(ABORT, 'forced setup failure');
            END;
            """));
        var draft = new SetupRequest(
            false,
            [new SetupUnitRow(2, null, "901", null, null)],
            [
                new SetupResidentRow(
                    2, null, "901", "Rollback User",
                    "rollback.setup@example.com", "+1 212 555 3333",
                    "Tenant", "Yes", "No")
            ]);

        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/confirm", draft);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await _host.WithDbAsync(async db =>
        {
            Assert.False(await db.Users.AnyAsync(item =>
                item.Email == "rollback.setup@example.com"));
            Assert.False(await db.Units.AnyAsync(item =>
                item.Identifier == "901"));
        });
    }

    [Fact]
    public async Task Confirm_revalidates_identity_changes_after_preview()
    {
        var draft = new SetupRequest(false, [],
        [
            new SetupResidentRow(
                2, null, null, "Late Conflict", "late.conflict@example.com",
                "+1 212 555 4444", null, null, null)
        ]);
        var previewResponse = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/preview", draft);
        Assert.Empty((await previewResponse.Content
            .ReadFromJsonAsync<SetupPreviewResponse>())!.Errors);

        await _host.WithServicesAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await userManager.CreateAsync(
                new ApplicationUser(
                    "Concurrent User", "concurrent@example.com",
                    "+1 212 555 4444"),
                "Existing123")).Succeeded);
        });

        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/setup/confirm", draft);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await _host.WithDbAsync(db => db.Users.AnyAsync(item =>
            item.Email == "late.conflict@example.com")));
    }

    [Fact]
    public async Task Permissions_are_limited_to_manager_scope_but_platform_admin_is_global()
    {
        var path =
            $"/condominiums/{_condominiumId}/setup/preview";
        var draft = new SetupRequest(true, [], []);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await _host.ClientFor(_residentId)
                .PostAsJsonAsync(path, draft)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await _host.ClientFor(_otherManagerId)
                .PostAsJsonAsync(path, draft)).StatusCode);

        var admin = _host.ClientFor(_residentId);
        admin.DefaultRequestHeaders.Add(
            "X-Test-Role", DependencyInjection.PlatformAdminRole);
        Assert.Equal(
            HttpStatusCode.OK,
            (await admin.PostAsJsonAsync(
                $"/condominiums/{_otherCondominiumId}/setup/preview",
                draft)).StatusCode);
    }

    private static ByteArrayContent File(string content, string fileName)
    {
        var result = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        result.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        return result;
    }

    private static object Tower(
        string name,
        int startFloor,
        int endFloor,
        int unitsPerFloor) => new
        {
            name,
            segments = new[]
            {
                new
                {
                    startFloor,
                    endFloor,
                    unitsPerFloor,
                    firstUnit = 1,
                    digits = 2,
                    includeFloorNumber = true,
                    prefix = "",
                    suffix = ""
                }
            }
        };

    private static byte[] CreateInlineStringXlsx(string[][] rows)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(
                   output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("xl/worksheets/sheet1.xml");
            using var writer = new StreamWriter(
                entry.Open(), new UTF8Encoding(false));
            writer.Write(
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                + "<worksheet xmlns=\"http://schemas.openxmlformats.org/"
                + "spreadsheetml/2006/main\"><sheetData>");
            for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                writer.Write($"<row r=\"{rowIndex + 1}\">");
                for (var column = 0; column < rows[rowIndex].Length; column++)
                {
                    var reference =
                        $"{(char)('A' + column)}{rowIndex + 1}";
                    writer.Write(
                        $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>"
                        + System.Security.SecurityElement.Escape(
                            rows[rowIndex][column])
                        + "</t></is></c>");
                }
                writer.Write("</row>");
            }
            writer.Write("</sheetData></worksheet>");
        }
        return output.ToArray();
    }

    private static byte[] FillResidentTemplate(byte[] template, string[] values)
    {
        using var output = new MemoryStream();
        output.Write(template); output.Position = 0;
        using (var archive = new ZipArchive(output, ZipArchiveMode.Update, true))
        {
            var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
            entry.Delete();
            var cells = string.Concat(values.Select((value, column) =>
                $"<c r=\"{(char)('A' + column)}2\" t=\"inlineStr\" s=\"1\"><is><t>"
                + System.Security.SecurityElement.Escape(value) + "</t></is></c>"));
            xml = xml.Replace("</sheetData>", $"<row r=\"2\">{cells}</row></sheetData>");
            using var writer = new StreamWriter(archive.CreateEntry("xl/worksheets/sheet1.xml").Open(), new UTF8Encoding(false));
            writer.Write(xml);
        }
        return output.ToArray();
    }
}
