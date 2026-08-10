using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.WhatsApp;

public sealed record AdministrativeUnitChoice(Guid Id, string Display);

public sealed partial class AdministrativeUnitResolver(AppDbContext db)
{
    public async Task<AdministrativeUnitChoice[]> ResolveAsync(
        Guid condominiumId, Guid? selectedUnitId, string? unitText,
        string? blockText, CancellationToken ct)
    {
        var candidates = await (from unit in db.Units.AsNoTracking()
            join block in db.CondominiumBlocks.AsNoTracking()
                on unit.BlockId equals block.Id into blocks
            from block in blocks.DefaultIfEmpty()
            where unit.CondominiumId == condominiumId && unit.IsActive
                && (!selectedUnitId.HasValue || unit.Id == selectedUnitId)
            orderby block == null ? "" : block.Identifier, unit.Identifier
            select new Candidate(unit.Id, unit.Identifier,
                block == null ? null : block.Identifier,
                block == null ? unit.Identifier
                    : $"Bloco {DisplayBlock(block.Identifier)} - {unit.Identifier}"))
            .ToArrayAsync(ct);
        if (selectedUnitId.HasValue)
            return candidates.Select(ToChoice).ToArray();
        if (string.IsNullOrWhiteSpace(unitText)) return [];

        var explicitUnit = NormalizeUnit(unitText);
        var explicitBlock = NormalizeBlock(blockText);
        var exact = candidates.Where(x => UnitEquals(x.Identifier, explicitUnit)
                && (explicitBlock is null || BlockEquals(x.Block, explicitBlock)))
            .Select(ToChoice).ToArray();
        if (exact.Length > 0) return exact;

        var parsed = ParseCombined(unitText);
        if (parsed is null) return [];
        var parsedBlock = explicitBlock ?? NormalizeBlock(parsed.Value.Block);
        return candidates.Where(x => UnitEquals(x.Identifier, parsed.Value.Unit)
                && parsedBlock is not null && BlockEquals(x.Block, parsedBlock))
            .Select(ToChoice).ToArray();
    }

    private static AdministrativeUnitChoice ToChoice(Candidate value) =>
        new(value.Id, value.Display);
    private static bool UnitEquals(string persisted, string supplied) =>
        string.Equals(NormalizeText(persisted), NormalizeText(supplied),
            StringComparison.Ordinal);
    private static bool BlockEquals(string? persisted, string supplied) =>
        persisted is not null && string.Equals(NormalizeBlock(persisted), supplied,
            StringComparison.Ordinal);
    private static string NormalizeUnit(string value)
    {
        var normalized = NormalizeText(value);
        return UnitPrefix().Replace(normalized, string.Empty).Trim();
    }
    private static string? NormalizeBlock(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = BlockPrefix().Replace(NormalizeText(value), string.Empty).Trim();
        if (normalized.All(char.IsAsciiDigit)
            && int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture,
                out var number))
            return number.ToString(CultureInfo.InvariantCulture);
        return normalized;
    }
    private static (string Unit, string Block)? ParseCombined(string value)
    {
        var normalized = NormalizeText(value);
        var slash = SlashExpression().Match(normalized);
        if (slash.Success)
            return (NormalizeUnit(slash.Groups["unit"].Value),
                slash.Groups["block"].Value);
        var unitFirst = UnitThenBlock().Match(normalized);
        if (unitFirst.Success)
            return (NormalizeUnit(unitFirst.Groups["unit"].Value),
                unitFirst.Groups["block"].Value);
        var blockFirst = BlockThenUnit().Match(normalized);
        return blockFirst.Success
            ? (NormalizeUnit(blockFirst.Groups["unit"].Value),
                blockFirst.Groups["block"].Value)
            : null;
    }
    private static string NormalizeText(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
    private static string DisplayBlock(string value) =>
        BlockPrefix().Replace(value.Trim(), string.Empty).Trim();

    [GeneratedRegex(@"^(?:apto|apartamento|unidade)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex UnitPrefix();
    [GeneratedRegex(@"^bloco\s+", RegexOptions.IgnoreCase)]
    private static partial Regex BlockPrefix();
    [GeneratedRegex(@"^(?<unit>[^/]+)\s*/\s*(?<block>[^/]+)$")]
    private static partial Regex SlashExpression();
    [GeneratedRegex(@"^(?:apto|apartamento|unidade)?\s*(?<unit>\S+)\s+(?:do\s+)?bloco\s+(?<block>\S+)$")]
    private static partial Regex UnitThenBlock();
    [GeneratedRegex(@"^bloco\s+(?<block>\S+)\s+(?:(?:apto|apartamento|unidade)\s+)?(?<unit>\S+)$")]
    private static partial Regex BlockThenUnit();

    private sealed record Candidate(Guid Id, string Identifier, string? Block,
        string Display);
}
