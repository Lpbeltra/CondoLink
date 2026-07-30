using Microsoft.AspNetCore.DataProtection;

namespace CondoLink.Api;

public static class DataProtectionConfiguration
{
    public const string SectionName = "DataProtection";

    public static IServiceCollection AddComvyDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var builder = services.AddDataProtection()
            .SetApplicationName("Comvy");
        var keysPath = configuration[$"{SectionName}:KeysPath"]?.Trim();
        if (string.IsNullOrEmpty(keysPath)) return services;

        var directory = new DirectoryInfo(Path.GetFullPath(keysPath));
        directory.Create();
        builder.PersistKeysToFileSystem(directory);
        return services;
    }
}
