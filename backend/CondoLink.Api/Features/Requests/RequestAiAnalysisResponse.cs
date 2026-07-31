using System.Text.Json;
using CondoLink.Domain.Entities;

namespace CondoLink.Api.Features.Requests;

public sealed record RequestAiAnalysisResponse(string Title, string Description,
    string? SuggestedCategory, double? Confidence, string[] MissingInformation,
    DateTime GeneratedAt, string? Model)
{
    public static RequestAiAnalysisResponse FromEntity(RequestAiAnalysis analysis) => new(
        analysis.GeneratedTitle,
        analysis.GeneratedDescription,
        analysis.SuggestedCategoryName,
        analysis.Confidence,
        JsonSerializer.Deserialize<string[]>(analysis.MissingInformationJson) ?? [],
        analysis.CreatedAt,
        analysis.AiModel);
}
