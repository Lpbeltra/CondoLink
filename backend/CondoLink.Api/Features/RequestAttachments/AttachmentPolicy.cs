namespace CondoLink.Api.Features.RequestAttachments;

public static class AttachmentPolicy
{
    public const int MaximumFileCount = 10;
    public const long MaximumFileSize = 15 * 1024 * 1024;
    public const long MaximumRequestSize = 96 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string[]> AllowedFiles =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"],
            [".png"] = ["image/png"],
            [".webp"] = ["image/webp"],
            [".mp4"] = ["video/mp4"],
            [".pdf"] = ["application/pdf"],
            [".ogg"] = ["audio/ogg"],
            [".opus"] = ["audio/ogg", "audio/opus"],
            [".mp3"] = ["audio/mpeg"],
            [".m4a"] = ["audio/mp4", "audio/x-m4a"],
            [".aac"] = ["audio/aac"],
            [".amr"] = ["audio/amr"]
        };

    public static ValidationResult Validate(string? fileName, long size, string? contentType)
    {
        var name = Path.GetFileName(fileName);
        var extension = Path.GetExtension(name) ?? string.Empty;
        var mediaType = contentType?.Split(';', 2)[0].Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
            return new("O nome do arquivo é inválido ou possui mais de 255 caracteres.");
        if (size <= 0) return new($"O arquivo “{name}” está vazio.");
        if (size > MaximumFileSize) return new("Cada arquivo pode possuir no máximo 15 MB.");
        if (string.IsNullOrWhiteSpace(mediaType)
            || !AllowedFiles.TryGetValue(extension, out var contentTypes)
            || !contentTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
            return new("No momento este tipo de arquivo ainda não é suportado.");
        return new(null, name, extension.ToLowerInvariant(), mediaType.ToLowerInvariant());
    }

    public static string? PreferredExtension(string? contentType) =>
        AllowedFiles.FirstOrDefault(x => x.Value.Contains(
            contentType ?? string.Empty, StringComparer.OrdinalIgnoreCase)).Key;

    public sealed record ValidationResult(
        string? Error,
        string? Name = null,
        string? Extension = null,
        string? ContentType = null);
}
