using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CondoLink.Domain.Enums;

namespace CondoLink.Api.Features.EmployeeDocuments;

/// <summary>
/// Splits one uploaded PDF's already-extracted page text into individual
/// documents and suggests which <see cref="Candidate"/> (employee) each one
/// belongs to. Never assumes one page equals one document: a page only starts
/// a new document when it carries its own identity signal (registration
/// number or name) different from the current one; pages without a signal
/// are treated as continuations of the current document.
///
/// Deliberately deterministic — no LLM call. Registration number and exact
/// name matches are strong signals; anything weaker is surfaced as a fuzzy,
/// low-confidence suggestion for a human to confirm or correct. Phone number
/// is never used as a matching signal.
/// </summary>
public static class EmployeeDocumentSplitter
{
    public sealed record Candidate(Guid EmployeeId, string FullName, string? NormalizedRegistrationNumber, string? JobTitle);
    public sealed record PageIdentity(Guid? EmployeeId, EmployeeDocumentIdentificationConfidence Confidence, EmployeeDocumentIdentificationMethod Method);
    public sealed record DocumentSegment(int PageStart, int PageEnd, Guid? EmployeeId,
        EmployeeDocumentIdentificationConfidence Confidence, EmployeeDocumentIdentificationMethod Method);

    public static IReadOnlyList<DocumentSegment> Segment(
        IReadOnlyList<(int PageNumber, string Text)> pages, IReadOnlyList<Candidate> candidates)
    {
        var segments = new List<DocumentSegment>();
        DocumentSegment? current = null;
        foreach (var page in pages.OrderBy(p => p.PageNumber))
        {
            var identity = DetectPageIdentity(page.Text, candidates);
            var isNewIdentity = identity.EmployeeId is not null
                && (current is null || identity.EmployeeId != current.EmployeeId);
            if (isNewIdentity)
            {
                if (current is not null) segments.Add(current);
                current = new DocumentSegment(page.PageNumber, page.PageNumber, identity.EmployeeId, identity.Confidence, identity.Method);
            }
            else if (current is null)
            {
                current = new DocumentSegment(page.PageNumber, page.PageNumber, null,
                    EmployeeDocumentIdentificationConfidence.None, EmployeeDocumentIdentificationMethod.None);
            }
            else
            {
                current = current with { PageEnd = page.PageNumber };
            }
        }
        if (current is not null) segments.Add(current);
        return segments;
    }

    public static PageIdentity DetectPageIdentity(string pageText, IReadOnlyList<Candidate> candidates)
    {
        var normalizedPage = Normalize(pageText);
        if (normalizedPage.Length == 0 || candidates.Count == 0)
            return new(null, EmployeeDocumentIdentificationConfidence.None, EmployeeDocumentIdentificationMethod.None);

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.NormalizedRegistrationNumber)) continue;
            var token = Normalize(candidate.NormalizedRegistrationNumber);
            if (token.Length < 2) continue;
            if (Regex.IsMatch(normalizedPage, $@"(?<![A-Z0-9]){Regex.Escape(token)}(?![A-Z0-9])"))
                return new(candidate.EmployeeId, EmployeeDocumentIdentificationConfidence.High, EmployeeDocumentIdentificationMethod.RegistrationNumber);
        }

        foreach (var candidate in candidates)
        {
            var normalizedName = Normalize(candidate.FullName);
            if (normalizedName.Length < 4) continue;
            if (normalizedPage.Contains(normalizedName, StringComparison.Ordinal))
                return new(candidate.EmployeeId, EmployeeDocumentIdentificationConfidence.High, EmployeeDocumentIdentificationMethod.ExactName);
        }

        Candidate? bestFuzzy = null;
        var bestScore = 0.0;
        foreach (var candidate in candidates)
        {
            var score = FuzzyNameScore(normalizedPage, candidate.FullName);
            if (score > bestScore) { bestScore = score; bestFuzzy = candidate; }
        }
        if (bestFuzzy is not null && bestScore >= 0.6)
            return new(bestFuzzy.EmployeeId, EmployeeDocumentIdentificationConfidence.Low, EmployeeDocumentIdentificationMethod.FuzzyName);

        return new(null, EmployeeDocumentIdentificationConfidence.None, EmployeeDocumentIdentificationMethod.None);
    }

    private static double FuzzyNameScore(string normalizedPage, string fullName)
    {
        var tokens = Normalize(fullName).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 3).ToArray();
        if (tokens.Length < 2) return 0;
        var matched = tokens.Count(token => Regex.IsMatch(normalizedPage, $@"(?<![A-Z]){Regex.Escape(token)}(?![A-Z])"));
        return (double)matched / tokens.Length;
    }

    internal static string Normalize(string value)
    {
        var withoutDiacritics = string.Concat(value.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
        return Regex.Replace(withoutDiacritics.ToUpperInvariant(), @"\s+", " ").Trim();
    }
}
