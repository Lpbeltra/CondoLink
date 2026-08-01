namespace CondoLink.Api.Features.RequestAttachments;

public sealed class LocalFileStorage
{
    private readonly string rootPath;

    public LocalFileStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredPath = configuration["FileStorage:RootPath"] ?? "storage";
        rootPath = Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath));
        Directory.CreateDirectory(rootPath);
    }

    public async Task<string> SaveAsync(Guid requestId, IFormFile file, string extension,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return await SaveAsync(requestId, stream, extension, cancellationToken);
    }

    public async Task<string> SaveAsync(Guid requestId, Stream content, string extension,
        CancellationToken cancellationToken)
    {
        var storageKey = Path.Combine("requests", requestId.ToString(), $"{Guid.NewGuid():N}{extension}")
            .Replace('\\', '/');
        var fullPath = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 81920, FileOptions.Asynchronous);
        await content.CopyToAsync(output, cancellationToken);
        return storageKey;
    }

    public async Task<string> SaveWhatsAppDraftAsync(
        Guid sessionId,
        ReadOnlyMemory<byte> content,
        string extension,
        CancellationToken cancellationToken)
    {
        var storageKey = Path.Combine(
                "whatsapp-drafts", sessionId.ToString(), $"{Guid.NewGuid():N}{extension}")
            .Replace('\\', '/');
        var fullPath = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var output = new FileStream(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous);
        await output.WriteAsync(content, cancellationToken);
        return storageKey;
    }

    public string PromoteWhatsAppDraft(
        Guid requestId,
        string temporaryStorageKey,
        string extension)
    {
        var source = Resolve(temporaryStorageKey);
        var destinationKey = Path.Combine(
                "requests", requestId.ToString(), $"{Guid.NewGuid():N}{extension}")
            .Replace('\\', '/');
        var destination = Resolve(destinationKey);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
        return destinationKey;
    }

    public FileStream? OpenRead(string storageKey)
    {
        var fullPath = Resolve(storageKey);
        return File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
    }

    public void Delete(string storageKey)
    {
        var fullPath = Resolve(storageKey);
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    private string Resolve(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || Path.IsPathRooted(storageKey))
            throw new InvalidOperationException("Invalid storage key.");

        var fullPath = Path.GetFullPath(Path.Combine(rootPath, storageKey));
        var rootPrefix = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage key.");
        return fullPath;
    }
}
