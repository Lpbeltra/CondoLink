using CondoLink.Api.Features.RequestAttachments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace CondoLink.Tests;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "condolink-storage-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Configured_root_survives_service_reconstruction_and_keeps_relative_keys()
    {
        var requestId = Guid.NewGuid();
        var first = Storage();
        var key = await first.SaveAsync(requestId,
            new MemoryStream([1, 2, 3]), ".pdf", default);

        Assert.StartsWith($"requests/{requestId}/", key);
        Assert.False(Path.IsPathRooted(key));
        await using var reopened = Storage().OpenRead(key);
        Assert.NotNull(reopened);
        using var buffer = new MemoryStream();
        await reopened.CopyToAsync(buffer);
        Assert.Equal([1, 2, 3], buffer.ToArray());
    }

    [Fact]
    public async Task Generated_keys_do_not_collide_for_equal_original_names()
    {
        var requestId = Guid.NewGuid();
        var storage = Storage();

        var first = await storage.SaveAsync(requestId,
            new MemoryStream([1]), ".jpg", default);
        var second = await storage.SaveAsync(requestId,
            new MemoryStream([2]), ".jpg", default);

        Assert.NotEqual(first, second);
        using var firstStream = storage.OpenRead(first);
        using var secondStream = storage.OpenRead(second);
        Assert.NotNull(firstStream);
        Assert.NotNull(secondStream);
    }

    [Fact]
    public void Path_traversal_and_absolute_paths_remain_blocked()
    {
        var storage = Storage();

        Assert.Throws<InvalidOperationException>(() => storage.OpenRead("../outside"));
        Assert.Throws<InvalidOperationException>(() => storage.OpenRead(
            Path.GetFullPath(Path.Combine(root, "..", "outside"))));
    }

    [Fact]
    public async Task WhatsApp_drafts_use_the_same_configured_root()
    {
        var storage = Storage();
        var sessionId = Guid.NewGuid();

        var key = await storage.SaveWhatsAppDraftAsync(sessionId,
            new byte[] { 4, 5, 6 }, ".ogg", default);

        Assert.StartsWith($"whatsapp-drafts/{sessionId}/", key);
        Assert.True(File.Exists(Path.Combine(root,
            key.Replace('/', Path.DirectorySeparatorChar))));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private LocalFileStorage Storage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:RootPath"] = root
            })
            .Build();
        return new LocalFileStorage(configuration, new TestEnvironment(root));
    }

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CondoLink.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
