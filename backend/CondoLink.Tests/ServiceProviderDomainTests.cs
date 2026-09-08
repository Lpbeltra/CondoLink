using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;

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
}
