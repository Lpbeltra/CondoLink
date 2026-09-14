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
    public sealed record Candidate(Guid EmployeeId, string FullName, string? NormalizedRegistrationNumber, string? JobTitle,
        string? NormalizedCpf = null, string? NormalizedCnpj = null);
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
            else if (identity.EmployeeId == current.EmployeeId && identity.EmployeeId is not null)
            {
                current = current with { PageEnd = page.PageNumber };
            }
            else if (LooksLikeExplicitContinuation(page.Text))
            {
                current = current with { PageEnd = page.PageNumber };
            }
            else
            {
                // Fail-safe: an ambiguous page is never silently attached to
                // the preceding employee's private payroll document.
                segments.Add(current);
                current = new DocumentSegment(page.PageNumber, page.PageNumber, null,
                    EmployeeDocumentIdentificationConfidence.None, EmployeeDocumentIdentificationMethod.None);
            }
        }
        if (current is not null) segments.Add(current);
        return segments;
    }

    public static PageIdentity DetectPageIdentity(string pageText, IReadOnlyList<Candidate> candidates)
    {
        var normalizedPage = Normalize(pageText);
        var compactPage = new string(normalizedPage.Where(char.IsLetterOrDigit).ToArray());
        if (normalizedPage.Length == 0 || candidates.Count == 0)
            return new(null, EmployeeDocumentIdentificationConfidence.None, EmployeeDocumentIdentificationMethod.None);

        var cpfMatches = candidates.Where(candidate =>
        {
            if (string.IsNullOrWhiteSpace(candidate.NormalizedCpf)) return false;
            return compactPage.Contains(new string(Normalize(candidate.NormalizedCpf).Where(char.IsDigit).ToArray()), StringComparison.Ordinal);
        }).ToArray();
        var hasCpf = Regex.Matches(compactPage, @"\d{11}").Count > 0;
        if (hasCpf && cpfMatches.Length == 1)
            return new(cpfMatches[0].EmployeeId, EmployeeDocumentIdentificationConfidence.High, EmployeeDocumentIdentificationMethod.Cpf);
        if (hasCpf)
            return new(null, EmployeeDocumentIdentificationConfidence.None, EmployeeDocumentIdentificationMethod.None);

        var cnpjTokens = candidates.Select(x => x.NormalizedCnpj).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        if (cnpjTokens.Length > 0 && Regex.Matches(compactPage, @"\d{14}").Count > 0
            && !cnpjTokens.Any(x => compactPage.Contains(new string(Normalize(x!).Where(char.IsDigit).ToArray()), StringComparison.Ordinal)))
            return new(null, EmployeeDocumentIdentificationConfidence.None, EmployeeDocumentIdentificationMethod.None);

        var registrationMatches = candidates.Where(candidate =>
        {
            if (string.IsNullOrWhiteSpace(candidate.NormalizedRegistrationNumber)) return false;
            var token = Normalize(candidate.NormalizedRegistrationNumber);
            return token.Length >= 2 && Regex.IsMatch(normalizedPage, $@"(?<![A-Z0-9]){Regex.Escape(token)}(?![A-Z0-9])");
        }).ToArray();
        if (registrationMatches.Length == 1)
            return new(registrationMatches[0].EmployeeId, EmployeeDocumentIdentificationConfidence.High, EmployeeDocumentIdentificationMethod.RegistrationNumber);
        if (registrationMatches.Length > 1)
            return new(null, EmployeeDocumentIdentificationConfidence.None, EmployeeDocumentIdentificationMethod.None);

        var exactNameMatches = candidates.Where(candidate =>
        {
            var normalizedName = Normalize(candidate.FullName);
            return normalizedName.Length >= 4 && normalizedPage.Contains(normalizedName, StringComparison.Ordinal);
        }).ToArray();
        if (exactNameMatches.Length == 1)
            return new(exactNameMatches[0].EmployeeId, EmployeeDocumentIdentificationConfidence.High, EmployeeDocumentIdentificationMethod.ExactName);
        if (exactNameMatches.Length > 1)
            return new(null, EmployeeDocumentIdentificationConfidence.None, EmployeeDocumentIdentificationMethod.None);

        Candidate? bestFuzzy = null;
        var bestScore = 0.0;
        foreach (var candidate in candidates)
        {
            var score = FuzzyNameScore(normalizedPage, candidate.FullName);
            if (score > bestScore) { bestScore = score; bestFuzzy = candidate; }
        }
        if (bestFuzzy is not null && bestScore >= 0.6 && IsCoherentNameMatch(normalizedPage, bestFuzzy.FullName))
            return new(bestFuzzy.EmployeeId, EmployeeDocumentIdentificationConfidence.Low, EmployeeDocumentIdentificationMethod.FuzzyName);

        return new(null, EmployeeDocumentIdentificationConfidence.None, EmployeeDocumentIdentificationMethod.None);
    }

    private static bool IsCoherentNameMatch(string normalizedPage, string fullName)
    {
        var tokens = Normalize(fullName).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2) return false;
        var first = tokens[0]; var last = tokens[^1];
        if (!ContainsToken(normalizedPage, first) || !ContainsToken(normalizedPage, last)) return false;
        var middle = tokens.Skip(1).SkipLast(1).Where(x => x.Length > 1).ToArray();
        // Intermediate names may be omitted by payroll exports; if present they
        // must be full tokens or initials, while the principal surname anchors
        // the suggestion.
        return middle.Length == 0 || middle.Any(token => ContainsToken(normalizedPage, token) || ContainsInitial(normalizedPage, token)) ||
            (middle.All(token => !ContainsToken(normalizedPage, token)) && ContainsToken(normalizedPage, last));
    }

    private static bool ContainsToken(string text, string token) => Regex.IsMatch(text, $@"(?<![A-Z]){Regex.Escape(token)}(?![A-Z])");
    private static bool ContainsInitial(string text, string token) => Regex.IsMatch(text, $@"(?<![A-Z]){Regex.Escape(token[..1])}(?:\.|\s)(?![A-Z])");

    private static bool LooksLikeExplicitContinuation(string text)
    {
        var normalized = Normalize(text);
        return normalized.Contains("CONTINUAC", StringComparison.Ordinal)
            || Regex.IsMatch(normalized, @"P[AÁ]GINA\s+\d+\s+DE\s+\d+");
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
