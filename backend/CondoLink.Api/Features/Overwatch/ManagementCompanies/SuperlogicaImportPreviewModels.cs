using CondoLink.Domain;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public sealed record SuperlogicaImportPreview(string ExternalCondominiumId, int ExternalRecordsRead,
    SuperlogicaImportPreviewSummary Summary, IReadOnlyList<SuperlogicaPreviewBlock> Blocks,
    IReadOnlyList<SuperlogicaPreviewUnit> UnitsWithoutBlock);
public sealed record SuperlogicaImportPreviewSummary(int ExternalRecordsRead, int Blocks, int Units,
    int Contacts, int NewUnits, int ExistingUnits, int Conflicts, int Ambiguous);
public sealed record SuperlogicaPreviewBlock(string Identifier, string Status, IReadOnlyList<SuperlogicaPreviewUnit> Units);
public sealed record SuperlogicaPreviewUnit(string ExternalUnitId, string Identifier, string? Block,
    string Status, string? ComvyIdentifier, IReadOnlyList<SuperlogicaPreviewContact> Contacts);
public sealed record SuperlogicaPreviewContact(IReadOnlyList<string> ExternalIds, string? Name, string? Email,
    IReadOnlyList<string> Phones, string? TaxIdMasked, IReadOnlyList<SuperlogicaPreviewRelationship> Relationships,
    string PersonStatus, string? ComvyName, string UnitRelationshipStatus);
public sealed record SuperlogicaPreviewRelationship(string? ExternalRelationshipType, string? EntryDate, string? ExitDate);

internal static class SuperlogicaImportPreviewBuilder
{
    public static SuperlogicaIdentityKeys GetIdentityKeys(IReadOnlyList<SuperlogicaUnitRow> rows)
    {
        var emails = rows.SelectMany(x => new[] { x.Email, x.OwnerEmail }).Where(x => x is not null)
            .Select(x => x!.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        var phones = rows.SelectMany(x => Phones(x.Phone, x.Fax, x.OwnerPhone, x.OwnerMobile)).Distinct(StringComparer.Ordinal).ToArray();
        var taxIds = rows.SelectMany(x => new[] { x.TaxId, x.OwnerTaxId }).Where(x => x is not null)
            .Select(x => RegistrationData.Digits(x)!).Distinct(StringComparer.Ordinal).ToArray();
        return new(emails, phones, taxIds);
    }

    public static SuperlogicaImportPreview Build(string externalCondominiumId,
        IReadOnlyList<SuperlogicaUnitRow> rows, IReadOnlyList<CondominiumBlock> comvyBlocks,
        IReadOnlyList<Unit> comvyUnits, IReadOnlyList<ApplicationUser> matchingUsers,
        IReadOnlyList<CondominiumMemberCandidate> condominiumMembers,
        IReadOnlyList<UnitMembership> unitMemberships)
    {
        var blocksByKey = comvyBlocks.GroupBy(x => Key(x.Identifier)).ToDictionary(x => x.Key, x => x.ToArray());
        var existingUnits = comvyUnits.Select(unit => new ExistingUnit(unit,
            unit.BlockId is Guid blockId ? comvyBlocks.FirstOrDefault(x => x.Id == blockId) : null)).ToArray();
        var groups = rows.Where(x => !string.IsNullOrWhiteSpace(x.ExternalUnitId))
            .GroupBy(x => x.ExternalUnitId!, StringComparer.Ordinal).ToArray();
        var drafts = groups.Select(group => MakeUnit(group.Key, group.ToArray(), existingUnits, blocksByKey,
            matchingUsers, condominiumMembers, unitMemberships)).ToArray();
        var duplicateKeys = drafts.GroupBy(x => MatchKey(x.Block, x.Identifier), StringComparer.Ordinal)
            .Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var draft in drafts)
            if (duplicateKeys.Contains(MatchKey(draft.Block, draft.Identifier)) && draft.Status is "New" or "Existing") draft.Status = "Conflict";

        var outputUnits = drafts.Select(x => x.ToModel()).ToArray();
        var outputBlocks = drafts.Where(x => x.Block is not null).GroupBy(x => Key(x.Block!))
            .Select(group =>
            {
                blocksByKey.TryGetValue(group.Key, out var matches);
                var status = matches is null ? "New" : matches.Length == 1 ? "Existing" : "Ambiguous";
                return new SuperlogicaPreviewBlock(group.First().Block!, status,
                    outputUnits.Where(unit => Key(unit.Block!) == group.Key).ToArray());
            }).OrderBy(x => x.Identifier, StringComparer.OrdinalIgnoreCase).ToArray();
        var withoutBlock = outputUnits.Where(x => x.Block is null).ToArray();
        var contacts = drafts.SelectMany(x => x.Contacts).ToArray();
        var unitStatuses = drafts.Select(x => x.Status).ToArray();
        var ambiguous = unitStatuses.Count(x => x == "Ambiguous") + contacts.Count(x => x.PersonStatus == "Ambiguous");
        var conflicts = unitStatuses.Count(x => x == "Conflict") + contacts.Count(x => x.PersonStatus == "Conflict");
        return new SuperlogicaImportPreview(externalCondominiumId, rows.Count,
            new SuperlogicaImportPreviewSummary(rows.Count, outputBlocks.Length, outputUnits.Length,
                contacts.Length, unitStatuses.Count(x => x == "New"), unitStatuses.Count(x => x == "Existing"), conflicts, ambiguous),
            outputBlocks, withoutBlock);
    }

    private static UnitDraft MakeUnit(string externalId, SuperlogicaUnitRow[] rows, ExistingUnit[] existingUnits,
        Dictionary<string, CondominiumBlock[]> blocksByKey, IReadOnlyList<ApplicationUser> users,
        IReadOnlyList<CondominiumMemberCandidate> members, IReadOnlyList<UnitMembership> links)
    {
        var identifiers = rows.Select(x => Clean(x.Identifier)).Where(x => x is not null).DistinctBy(Key).ToArray();
        var blocks = rows.Select(x => Clean(x.Block)).Where(x => x is not null).DistinctBy(Key).ToArray();
        var identifier = identifiers.FirstOrDefault() ?? externalId;
        var block = blocks.FirstOrDefault();
        var keyInconsistent = identifiers.Length > 1 || blocks.Length > 1;
        var candidates = MatchUnits(block, identifier, existingUnits, blocksByKey);
        var status = keyInconsistent ? "Conflict" : candidates.Length switch
        {
            0 => "New",
            1 => "Existing",
            _ => "Ambiguous"
        };
        var match = candidates.Length == 1 ? candidates[0].Unit : null;
        var contacts = MakeContacts(rows, match, users, members, links);
        return new UnitDraft(externalId, identifier, block, status, match?.Identifier, contacts);
    }

    private static ExistingUnit[] MatchUnits(string? block, string identifier, ExistingUnit[] units,
        Dictionary<string, CondominiumBlock[]> blocksByKey)
    {
        var blockId = (Guid?)null;
        if (block is not null)
        {
            if (!blocksByKey.TryGetValue(Key(block), out var blocks) || blocks.Length == 0) return [];
            if (blocks.Length > 1) return units.Where(x => x.Block is not null && Key(x.Block.Identifier) == Key(block) && Key(x.Unit.Identifier) == Key(identifier)).ToArray();
            blockId = blocks[0].Id;
        }
        return units.Where(x => x.Unit.BlockId == blockId && Key(x.Unit.Identifier) == Key(identifier)).ToArray();
    }

    private static IReadOnlyList<SuperlogicaPreviewContact> MakeContacts(SuperlogicaUnitRow[] rows, Unit? matchedUnit,
        IReadOnlyList<ApplicationUser> users, IReadOnlyList<CondominiumMemberCandidate> members,
        IReadOnlyList<UnitMembership> links)
    {
        var candidates = new List<ContactDraft>();
        var serial = 0;
        foreach (var row in rows)
        {
            var contact = new ContactDraft(row.ExternalContactId, "contato", row.Name, row.Email,
                Phones(row.Phone, row.Fax), row.TaxId,
                new(row.RelationshipType, row.EntryDate, row.ExitDate), serial++);
            if (contact.HasData) AddOrMerge(candidates, contact);
            var owner = new ContactDraft(row.ExternalOwnerId, "proprietario", row.OwnerName, row.OwnerEmail,
                Phones(row.OwnerPhone, row.OwnerMobile), row.OwnerTaxId,
                new(row.OwnerType, null, null), serial++);
            if (owner.HasData) AddOrMerge(candidates, owner);
        }
        foreach (var candidate in candidates)
        {
            candidate.NameOnlyAmbiguous = candidate.Name is not null && candidates.Any(other => other != candidate
                && Key(other.Name ?? "") == Key(candidate.Name) && !SharesStrongIdentity(candidate, other));
            var matches = MatchPerson(candidate, users);
            if (candidate.IdentityConflict || matches.Length > 1) candidate.PersonStatus = "Conflict";
            else if (matches.Length == 1)
            {
                candidate.PersonStatus = matches[0].IsActive && (candidate.Name is null
                    || Key(candidate.Name) == Key(matches[0].FullName)) ? "Existing" : "Conflict";
                candidate.ComvyName = matches[0].FullName;
                candidate.MatchedUserId = matches[0].Id;
            }
            else if (candidate.NameOnlyAmbiguous || members.Any(x => Key(x.Name) == Key(candidate.Name ?? ""))) candidate.PersonStatus = "Ambiguous";
            else candidate.PersonStatus = "New";
            candidate.UnitRelationshipStatus = candidate.MatchedUserId is Guid userId && matchedUnit is not null
                ? links.Any(x => x.IsActive && x.UnitId == matchedUnit.Id && x.UserId == userId) ? "NoChange" : "New"
                : candidate.PersonStatus is "Conflict" or "Ambiguous" ? candidate.PersonStatus : "New";
        }
        return candidates.Select(x => new SuperlogicaPreviewContact(x.ExternalIds.Order(StringComparer.Ordinal).ToArray(),
            x.Name, x.Email, x.Phones.Order(StringComparer.Ordinal).ToArray(), MaskTaxId(x.TaxId),
            x.Relationships.Distinct().ToArray(), x.PersonStatus, x.ComvyName, x.UnitRelationshipStatus)).ToArray();
    }

    private static void AddOrMerge(List<ContactDraft> candidates, ContactDraft incoming)
    {
        var exact = incoming.ExternalId is null ? null : candidates.FirstOrDefault(x => x.Source == incoming.Source && x.ExternalIds.Contains(incoming.ExternalId));
        var byStrongIdentity = candidates.Where(x => SharesStrongIdentity(x, incoming)).ToArray();
        var target = exact ?? (byStrongIdentity.Length == 1 ? byStrongIdentity[0] : null);
        if (target is null) { candidates.Add(incoming); return; }
        if (target.Name is not null && incoming.Name is not null && Key(target.Name) != Key(incoming.Name)) target.IdentityConflict = true;
        if (target.Email is not null && incoming.Email is not null && Key(target.Email) != Key(incoming.Email)) target.IdentityConflict = true;
        if (target.TaxId is not null && incoming.TaxId is not null && target.TaxId != incoming.TaxId) target.IdentityConflict = true;
        target.Name ??= incoming.Name; target.Email ??= incoming.Email; target.TaxId ??= incoming.TaxId;
        target.Phones.UnionWith(incoming.Phones); target.Relationships.Add(incoming.Relationships[0]);
        target.ExternalIds.UnionWith(incoming.ExternalIds);
    }

    private static ApplicationUser[] MatchPerson(ContactDraft contact, IReadOnlyList<ApplicationUser> users)
    {
        var candidates = new HashSet<ApplicationUser>();
        foreach (var user in users)
        {
            if (contact.Email is not null && string.Equals(user.Email?.Trim(), contact.Email, StringComparison.OrdinalIgnoreCase)
                || contact.TaxId is not null && RegistrationData.Digits(user.Cpf) == contact.TaxId
                || contact.Phones.Any(phone => phone == user.NormalizedPhoneNumber)) candidates.Add(user);
        }
        return candidates.ToArray();
    }

    private static bool SharesStrongIdentity(ContactDraft first, ContactDraft second) =>
        first.TaxId is not null && first.TaxId == second.TaxId
        || first.Email is not null && second.Email is not null && string.Equals(first.Email, second.Email, StringComparison.OrdinalIgnoreCase)
        || first.Name is not null && Key(first.Name) == Key(second.Name ?? "") && first.Phones.Overlaps(second.Phones);

    private static HashSet<string> Phones(params string?[] values)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x)))
            foreach (var part in value!.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                if (PhoneNumberNormalizer.Normalize(part) is { } normalized) result.Add(normalized);
        return result;
    }

    private static string? MaskTaxId(string? value)
    {
        if (value is null) return null;
        var digits = RegistrationData.Digits(value);
        return digits?.Length == 11 ? $"***.***.***-{digits[^2..]}" : digits?.Length == 14 ? $"**.***.***/****-{digits[^2..]}" : "***";
    }

    private static string? Clean(string? value) => RegistrationData.Optional(value);
    private static string Key(string? value) => string.Join(' ', (value ?? "").Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    private static string MatchKey(string? block, string identifier) => $"{Key(block ?? "")}|{Key(identifier)}";

    private sealed record ExistingUnit(Unit Unit, CondominiumBlock? Block);
    private sealed class UnitDraft(string externalId, string identifier, string? block, string status, string? comvyIdentifier, IReadOnlyList<SuperlogicaPreviewContact> contacts)
    {
        public string ExternalId { get; } = externalId;
        public string Identifier { get; } = identifier;
        public string? Block { get; } = block;
        public string Status { get; set; } = status;
        public IReadOnlyList<SuperlogicaPreviewContact> Contacts { get; } = contacts;
        public SuperlogicaPreviewUnit ToModel() => new(ExternalId, Identifier, Block, Status, comvyIdentifier, Contacts);
    }
    private sealed class ContactDraft(string? externalId, string source, string? name, string? email, HashSet<string> phones,
        string? taxId, SuperlogicaPreviewRelationship relationship, int serial)
    {
        public string? ExternalId { get; } = externalId;
        public HashSet<string> ExternalIds { get; } = externalId is null ? [] : [externalId];
        public string Source { get; } = source;
        public string? Name { get; set; } = Clean(name);
        public string? Email { get; set; } = Clean(email)?.ToLowerInvariant();
        public HashSet<string> Phones { get; } = phones;
        public string? TaxId { get; set; } = RegistrationData.Digits(taxId);
        public List<SuperlogicaPreviewRelationship> Relationships { get; } = [relationship];
        public int Serial { get; } = serial;
        public bool IdentityConflict { get; set; }
        public bool NameOnlyAmbiguous { get; set; }
        public string PersonStatus { get; set; } = "New";
        public string UnitRelationshipStatus { get; set; } = "New";
        public string? ComvyName { get; set; }
        public Guid? MatchedUserId { get; set; }
        public bool HasData => ExternalId is not null || Name is not null || Email is not null || Phones.Count > 0 || TaxId is not null;
    }
}

internal sealed record CondominiumMemberCandidate(Guid UserId, string Name);
internal sealed record SuperlogicaIdentityKeys(string[] Emails, string[] Phones, string[] TaxIds);
