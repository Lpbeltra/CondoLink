using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using System.Text.Json;

namespace CondoLink.Tests;

public sealed class ServiceProviderDomainTests
{
    [Fact]
    public void Creates_active_provider_with_pix_and_updates_status()
    {
        var now = DateTime.UtcNow;
        var provider = new ServiceProvider("César", "Hidro Ltda", "Hidráulica", "César", "+5511999999999", "cesar@example.com", "123", ServiceProviderPixKeyType.Cpf, "Notas", now);
        Assert.True(provider.IsActive);
        Assert.Equal(ServiceProviderPixKeyType.Cpf, provider.PixKeyType);
        provider.Deactivate(now.AddMinutes(1));
        Assert.False(provider.IsActive);
        provider.Activate(now.AddMinutes(2));
        Assert.True(provider.IsActive);
    }

    [Fact]
    public void Requires_pix_type_when_pix_key_is_present()
    {
        Assert.Throws<ArgumentException>(() => new ServiceProvider("A", null, "Elétrica", null, "5511999999999", null, "key", null, null, DateTime.UtcNow));
    }

    [Fact]
    public void Accepts_the_string_pix_contract_used_by_the_management_form()
    {
        var pixType = JsonSerializer.Deserialize<ServiceProviderPixKeyType>("\"Cpf\"");

        Assert.Equal(ServiceProviderPixKeyType.Cpf, pixType);
    }

    [Fact]
    public void Specialty_normalizes_comparison_without_changing_the_display_name()
    {
        var specialty = new ServiceProviderSpecialty(Guid.NewGuid(), "  HidrÃ¡ulica  ");

        Assert.Equal("HidrÃ¡ulica", specialty.Name);
        Assert.Equal(specialty.Name.ToUpperInvariant(), specialty.NormalizedName);
    }
}
