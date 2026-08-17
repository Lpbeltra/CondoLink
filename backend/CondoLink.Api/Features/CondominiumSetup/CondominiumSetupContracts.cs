namespace CondoLink.Api.Features.CondominiumSetup;

public sealed record SetupRequest(
    bool NoRegistrableUnits,
    IReadOnlyList<SetupUnitRow>? Units,
    IReadOnlyList<SetupResidentRow>? Residents);

public sealed record SetupUnitRow(
    int Line,
    string? Block,
    string? Unit,
    string? Floor,
    string? Description);

public sealed record SetupResidentRow(
    int Line,
    string? Block,
    string? Unit,
    string? Name,
    string? Email,
    string? Phone,
    string? Relationship,
    string? Resident,
    string? PrimaryResidence);

public sealed record SetupIssue(int Line, string Column, string Reason);

public sealed record SetupBlockPreview(string Identifier, bool Existing);

public sealed record SetupUnitPreview(
    int Line,
    string? Block,
    string Unit,
    string? Floor,
    string? Description,
    bool Existing);

public sealed record SetupResidentPreview(
    int Line,
    string? Block,
    string? Unit,
    string Name,
    string Email,
    string? Phone,
    string? Relationship,
    bool Resident,
    bool PrimaryResidence,
    bool ExistingUser,
    string Status = "Ready",
    string? NormalizedPhone = null,
    Guid? ExistingUserId = null);

public sealed record SetupTotals(
    int Blocks,
    int Units,
    int Residents,
    int ExistingUsers,
    int NewUsers);

public sealed record SetupPreviewResponse(
    SetupRequest Draft,
    IReadOnlyList<SetupBlockPreview> Blocks,
    IReadOnlyList<SetupUnitPreview> Units,
    IReadOnlyList<SetupResidentPreview> Residents,
    IReadOnlyList<SetupIssue> Warnings,
    IReadOnlyList<SetupIssue> Errors,
    SetupTotals Totals);

public sealed record SetupCredential(
    Guid UserId,
    string FullName,
    string Email,
    string TemporaryPassword);

public sealed record SetupConfirmationResponse(
    int BlocksCreated,
    int UnitsCreated,
    int ResidentsLinked,
    IReadOnlyList<SetupCredential> Credentials,
    string Message,
    int UsersCreated = 0,
    int UsersReused = 0,
    int MembershipsCreated = 0,
    int MembershipsExisting = 0,
    int LinesIgnored = 0,
    int Warnings = 0);

public sealed record SetupGeneratorRequest(
    IReadOnlyList<SetupGeneratorTower>? Towers,
    IReadOnlyList<SetupResidentRow>? Residents);

public sealed record SetupGeneratorTower(
    string? Name,
    IReadOnlyList<SetupGeneratorSegment>? Segments);

public sealed record SetupGeneratorSegment(
    int StartFloor,
    int EndFloor,
    int UnitsPerFloor,
    int FirstUnit,
    int Digits,
    bool IncludeFloorNumber,
    string? Prefix,
    string? Suffix);
