namespace CondoLink.Domain.Entities;

public sealed class RequestAiAnalysis
{
    private RequestAiAnalysis() { }

    public RequestAiAnalysis(Guid requestId, string generatedTitle,
        string generatedDescription, string? suggestedCategoryName,
        double? confidence, string missingInformationJson, string? aiModel)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId is required.", nameof(requestId));
        if (string.IsNullOrWhiteSpace(generatedTitle))
            throw new ArgumentException("GeneratedTitle is required.", nameof(generatedTitle));
        if (string.IsNullOrWhiteSpace(generatedDescription))
            throw new ArgumentException("GeneratedDescription is required.", nameof(generatedDescription));
        if (string.IsNullOrWhiteSpace(missingInformationJson))
            throw new ArgumentException("MissingInformationJson is required.", nameof(missingInformationJson));
        if (confidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        Id = Guid.NewGuid();
        RequestId = requestId;
        GeneratedTitle = generatedTitle.Trim();
        GeneratedDescription = generatedDescription.Trim();
        SuggestedCategoryName = Optional(suggestedCategoryName);
        Confidence = confidence;
        MissingInformationJson = missingInformationJson;
        AiModel = Optional(aiModel);
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public string GeneratedTitle { get; private set; } = null!;
    public string GeneratedDescription { get; private set; } = null!;
    public string? SuggestedCategoryName { get; private set; }
    public double? Confidence { get; private set; }
    public string MissingInformationJson { get; private set; } = null!;
    public string? AiModel { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
