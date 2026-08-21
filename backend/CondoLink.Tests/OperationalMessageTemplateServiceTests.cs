using CondoLink.Api.Features.OperationalMessages;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

public sealed class OperationalMessageTemplateServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private OperationalMessageTemplateService _service = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _service = new OperationalMessageTemplateService(_db);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }

    [Fact]
    public async Task Uses_code_default_without_database_record()
    {
        var result = await _service.ComposeAsync("WaitingForThirdParty", "Maria",
            "Residencial Exemplo", "Conteúdo literal çã", default);
        Assert.Contains("Olá, Maria!", result);
        Assert.Contains("Conteúdo literal çã", result);
        Assert.Contains("enviar \"Oi\"", result);
    }

    [Fact]
    public async Task Global_override_replaces_only_editable_frame_and_can_be_restored()
    {
        var entity = new OperationalMessageTemplate("Resolved",
            "Oi, {PrimeiroNome}, do {NomeCondominio}.", "Até logo.", Guid.NewGuid(), DateTime.UtcNow);
        _db.Add(entity); await _db.SaveChangesAsync();
        var overridden = await _service.ComposeAsync("Resolved", "Maria", "Condomínio Águas",
            "ADMIN {PrimeiroNome}", default);
        Assert.Contains("Oi, Maria, do Condomínio Águas.", overridden);
        Assert.Contains("ADMIN {PrimeiroNome}", overridden); // administrative content stays literal
        _db.Remove(entity); await _db.SaveChangesAsync();
        var restored = await _service.ComposeAsync("Resolved", "Maria", "Condomínio Águas", "ADMIN", default);
        Assert.Contains("finalizada pela administração", restored);
        Assert.DoesNotContain("Até logo", restored);
    }

    [Theory]
    [InlineData("WaitingForThirdParty")]
    [InlineData("WaitingForResident")]
    [InlineData("WaitingForResidentClosure")]
    [InlineData("Resolved")]
    [InlineData("Cancelled")]
    [InlineData("Reopened")]
    public async Task Every_supported_trigger_composes_without_truncation(string key)
    {
        var administrative = new string('á', 1000);
        var result = await _service.ComposeAsync(key, "Maria", "Residencial Exemplo", administrative, default);
        Assert.Contains(administrative, result);
        Assert.True(result.Length <= OperationalMessageTemplateService.OutboundMaximumLength);
        if (key == "WaitingForResident") Assert.Contains("Responda por aqui para continuar.", result);
        if (key == "WaitingForResidentClosure")
        {
            Assert.Contains("1 - Sim, finalizar atendimento", result);
            Assert.Contains("2 - Ainda tenho uma dúvida", result);
        }
    }

    [Fact]
    public void Rejects_unknown_and_structural_placeholders_and_oversized_parts()
    {
        Assert.Contains("não permitido", OperationalMessageTemplateService.Validate("{Token}", ""));
        Assert.Contains("estrutural", OperationalMessageTemplateService.Validate("{MensagemDoSindico}", ""));
        Assert.Contains("máximo", OperationalMessageTemplateService.Validate(new string('x', 1201), ""));
        Assert.Null(OperationalMessageTemplateService.Validate("{PrimeiroNome}", "{NomeCondominio}"));
    }
}
