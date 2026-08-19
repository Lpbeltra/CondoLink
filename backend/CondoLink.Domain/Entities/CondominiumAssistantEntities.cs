using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class CondominiumDocument
{
    private CondominiumDocument() { }
    public CondominiumDocument(Guid condominiumId, string name, CondominiumDocumentType type,
        string originalFileName, string storageKey, string mimeType, int version,
        DateOnly? documentDate, Guid uploadedByUserId)
    {
        Id = Guid.NewGuid(); CondominiumId = condominiumId; Name = name.Trim(); DocumentType = type;
        OriginalFileName = originalFileName; StorageKey = storageKey; MimeType = mimeType;
        Version = version; DocumentDate = documentDate; UploadedByUserId = uploadedByUserId;
        IsActive = true; ProcessingStatus = CondominiumDocumentProcessingStatus.Pending;
        CreatedAt = UpdatedAt = DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid CondominiumId { get; private set; }
    public string Name { get; private set; } = null!;
    public CondominiumDocumentType DocumentType { get; private set; }
    public string OriginalFileName { get; private set; } = null!;
    public string StorageKey { get; private set; } = null!;
    public string MimeType { get; private set; } = null!;
    public int Version { get; private set; }
    public DateOnly? DocumentDate { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public CondominiumDocumentProcessingStatus ProcessingStatus { get; private set; }
    public string? ProcessingError { get; private set; }
    public void Processing() { ProcessingStatus = CondominiumDocumentProcessingStatus.Processing; ProcessingError = null; UpdatedAt = DateTime.UtcNow; }
    public void SetStorageKey(string storageKey) { StorageKey = storageKey; UpdatedAt = DateTime.UtcNow; }
    public void Ready() { ProcessingStatus = CondominiumDocumentProcessingStatus.Ready; ProcessingError = null; UpdatedAt = DateTime.UtcNow; }
    public void Fail(string error, bool unsupported = false) { ProcessingStatus = unsupported ? CondominiumDocumentProcessingStatus.Unsupported : CondominiumDocumentProcessingStatus.Failed; ProcessingError = error[..Math.Min(error.Length, 500)]; UpdatedAt = DateTime.UtcNow; }
    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.UtcNow; }
}

public sealed class CondominiumDocumentChunk
{
    private CondominiumDocumentChunk() { }
    public CondominiumDocumentChunk(Guid documentId, Guid condominiumId, int index,
        string content, string embedding, int? pageNumber, string? sectionTitle,
        string embeddingModel = "local-feature-hash-v1")
    { Id = Guid.NewGuid(); CondominiumDocumentId = documentId; CondominiumId = condominiumId;
      ChunkIndex = index; Content = content; Embedding = embedding; PageNumber = pageNumber;
      SectionTitle = sectionTitle; EmbeddingModel = embeddingModel; CreatedAt = DateTime.UtcNow; }
    public Guid Id { get; private set; }
    public Guid CondominiumDocumentId { get; private set; }
    public Guid CondominiumId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string Content { get; private set; } = null!;
    public string Embedding { get; private set; } = null!;
    public string EmbeddingModel { get; private set; } = null!;
    public int? PageNumber { get; private set; }
    public string? SectionTitle { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

public sealed class CondominiumDocumentKnowledge
{
    private CondominiumDocumentKnowledge() { }
    public CondominiumDocumentKnowledge(Guid documentId, Guid condominiumId, string summary,
        string topicsJson, string entitiesJson, string datesJson, string factsJson, string searchText,
        string analyzerVersion)
    { Id = Guid.NewGuid(); CondominiumDocumentId = documentId; CondominiumId = condominiumId;
      Summary = summary; TopicsJson = topicsJson; EntitiesJson = entitiesJson; DatesJson = datesJson;
      FactsJson = factsJson; SearchText = searchText; AnalyzerVersion = analyzerVersion;
      CreatedAt = UpdatedAt = DateTime.UtcNow; }
    public Guid Id { get; private set; }
    public Guid CondominiumDocumentId { get; private set; }
    public Guid CondominiumId { get; private set; }
    public string Summary { get; private set; } = null!;
    public string TopicsJson { get; private set; } = null!;
    public string EntitiesJson { get; private set; } = null!;
    public string DatesJson { get; private set; } = null!;
    public string FactsJson { get; private set; } = null!;
    public string SearchText { get; private set; } = null!;
    public string AnalyzerVersion { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}

public sealed class CondominiumAssistantConversation
{
    private CondominiumAssistantConversation() { }
    public CondominiumAssistantConversation(Guid condominiumId, Guid userId, Guid? requestId, string title)
    { Id = Guid.NewGuid(); CondominiumId = condominiumId; CreatedByUserId = userId; RequestId = requestId;
      Title = title.Trim(); CreatedAt = UpdatedAt = DateTime.UtcNow; }
    public Guid Id { get; private set; }
    public Guid CondominiumId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? RequestId { get; private set; }
    public string Title { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public void RemoveRequestContext() { RequestId = null; UpdatedAt = DateTime.UtcNow; }
    public void Touch() => UpdatedAt = DateTime.UtcNow;
    public void SetInitialTitle(string title)
    {
        if (Title == "Nova conversa") Title = title.Trim()[..Math.Min(title.Trim().Length, 60)];
    }
}

public sealed class CondominiumAssistantMessage
{
    private CondominiumAssistantMessage() { }
    public CondominiumAssistantMessage(Guid conversationId, CondominiumAssistantRole role,
        string content, string? sourcesJson = null)
    { Id = Guid.NewGuid(); ConversationId = conversationId; Role = role; Content = content;
      SourcesJson = sourcesJson; CreatedAt = DateTime.UtcNow; }
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public CondominiumAssistantRole Role { get; private set; }
    public string Content { get; private set; } = null!;
    public string? SourcesJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
