using CondoLink.Api;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class DataProtectionConfigurationTests
{
    [Fact]
    public void Persisted_keys_decrypt_after_provider_recreation()
    {
        var keysPath = Path.Combine(
            Path.GetTempPath(), $"comvy-data-protection-{Guid.NewGuid():N}");
        try
        {
            var protectedValue = Protect(keysPath, "sensitive-message");

            using var secondProvider = Services(keysPath);
            var protector = secondProvider
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("test-purpose");

            Assert.Equal(
                "sensitive-message",
                protector.Unprotect(protectedValue));
            Assert.NotEmpty(Directory.GetFiles(keysPath));
        }
        finally
        {
            if (Directory.Exists(keysPath))
                Directory.Delete(keysPath, recursive: true);
        }
    }

    private static string Protect(string keysPath, string value)
    {
        using var provider = Services(keysPath);
        return provider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("test-purpose")
            .Protect(value);
    }

    private static ServiceProvider Services(string keysPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = keysPath
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddComvyDataProtection(configuration);
        return services.BuildServiceProvider();
    }
}
