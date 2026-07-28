using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.CondominiumSetup;

public static class CondominiumSetupEndpoints
{
    private static readonly string[] StructureHeaders =
        ["Block", "Unit", "Floor", "Description"];
    private static readonly string[] ResidentHeaders =
    [
        "Block", "Unit", "Name", "Email", "Phone", "Relationship",
        "Resident", "PrimaryResidence"
    ];

    public static IEndpointRouteBuilder MapCondominiumSetup(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/condominiums/{condominiumId:guid}/setup")
            .RequireAuthorization();
        group.MapGet("/templates/{template}", DownloadTemplateAsync);
        group.MapPost("/import/preview", ImportPreviewAsync);
        group.MapPost("/generate/preview", GeneratePreviewAsync);
        group.MapPost("/preview", PreviewAsync);
        group.MapPost("/confirm", ConfirmAsync);
        return endpoints;
    }

    private static async Task<IResult> DownloadTemplateAsync(
        Guid condominiumId,
        string template,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var access = await CheckAccessAsync(
            condominiumId, principal, db, cancellationToken);
        if (access is not null) return access;

        var (fileName, content) = template.ToLowerInvariant() switch
        {
            "structure" => (
                "condolink-estrutura.csv",
                "Block,Unit,Floor,Description\r\n"
                + "Tower A,101,1,\r\n"
                + ",House 4,,Residential house\r\n"),
            "residents" => (
                "condolink-moradores.csv",
                "Block,Unit,Name,Email,Phone,Relationship,Resident,PrimaryResidence\r\n"
                + "Tower A,101,Maria Silva,maria@example.com,11999999999,Owner,Yes,Yes\r\n"),
            _ => (string.Empty, string.Empty)
        };
        if (fileName.Length == 0)
            return Results.NotFound(new { error = "Modelo não encontrado." });

        return Results.File(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
                .GetBytes(content),
            "text/csv; charset=utf-8",
            fileName);
    }

    private static async Task<IResult> ImportPreviewAsync(
        Guid condominiumId,
        HttpRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var access = await CheckAccessAsync(
            condominiumId, principal, db, cancellationToken);
        if (access is not null) return access;
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new
            {
                error = "Envie as planilhas usando multipart/form-data."
            });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var structureFile = form.Files.GetFile("structureFile");
        var residentsFile = form.Files.GetFile("residentsFile");
        if (structureFile is null && residentsFile is null)
            return Results.BadRequest(
                new { error = "Selecione ao menos uma planilha." });

        var units = new List<SetupUnitRow>();
        var residents = new List<SetupResidentRow>();
        if (structureFile is not null)
        {
            var result = await SetupSpreadsheetReader.ReadAsync(
                structureFile, StructureHeaders, cancellationToken);
            if (result.Error is not null)
                return Results.BadRequest(new { error = result.Error });
            units.AddRange(result.Rows.Select(row => new SetupUnitRow(
                row.Line,
                row.Values["Block"],
                row.Values["Unit"],
                row.Values["Floor"],
                row.Values["Description"])));
        }

        if (residentsFile is not null)
        {
            var result = await SetupSpreadsheetReader.ReadAsync(
                residentsFile, ResidentHeaders, cancellationToken);
            if (result.Error is not null)
                return Results.BadRequest(new { error = result.Error });
            residents.AddRange(result.Rows.Select(row => new SetupResidentRow(
                row.Line,
                row.Values["Block"],
                row.Values["Unit"],
                row.Values["Name"],
                row.Values["Email"],
                row.Values["Phone"],
                row.Values["Relationship"],
                row.Values["Resident"],
                row.Values["PrimaryResidence"])));
        }

        var noUnits = string.Equals(
            form["noRegistrableUnits"].FirstOrDefault(),
            "true",
            StringComparison.OrdinalIgnoreCase);
        return Results.Ok(await BuildPreviewAsync(
            condominiumId,
            new SetupRequest(noUnits, units, residents),
            db,
            cancellationToken));
    }

    private static async Task<IResult> PreviewAsync(
        Guid condominiumId,
        SetupRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var access = await CheckAccessAsync(
            condominiumId, principal, db, cancellationToken);
        if (access is not null) return access;
        return Results.Ok(await BuildPreviewAsync(
            condominiumId, request, db, cancellationToken));
    }

    private static async Task<IResult> GeneratePreviewAsync(
        Guid condominiumId,
        SetupGeneratorRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var access = await CheckAccessAsync(
            condominiumId, principal, db, cancellationToken);
        if (access is not null) return access;
        var generation = GenerateUnits(request.Towers ?? []);
        if (generation.Errors.Count > 0)
            return Results.BadRequest(new { errors = generation.Errors });
        return Results.Ok(await BuildPreviewAsync(
            condominiumId,
            new SetupRequest(
                false,
                generation.Units,
                request.Residents ?? []),
            db,
            cancellationToken));
    }

    private static async Task<IResult> ConfirmAsync(
        Guid condominiumId,
        SetupRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var access = await CheckAccessAsync(
            condominiumId, principal, db, cancellationToken);
        if (access is not null) return access;
        var preview = await BuildPreviewAsync(
            condominiumId, request, db, cancellationToken);
        if (preview.Errors.Count > 0)
            return Results.BadRequest(preview);

        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var blocks = await db.CondominiumBlocks
                .Where(item => item.CondominiumId == condominiumId)
                .ToDictionaryAsync(
                    item => item.Identifier,
                    StringComparer.OrdinalIgnoreCase,
                    cancellationToken);
            var blocksCreated = 0;
            foreach (var blockPreview in preview.Blocks.Where(
                         item => !item.Existing))
            {
                var block = new CondominiumBlock(
                    condominiumId, blockPreview.Identifier);
                db.CondominiumBlocks.Add(block);
                blocks[block.Identifier] = block;
                blocksCreated++;
            }

            var existingUnits = await (
                    from unit in db.Units
                    join block in db.CondominiumBlocks
                        on unit.BlockId equals block.Id into unitBlocks
                    from block in unitBlocks.DefaultIfEmpty()
                    where unit.CondominiumId == condominiumId
                    select new { Unit = unit, Block = block })
                .ToListAsync(cancellationToken);
            var units = existingUnits.ToDictionary(
                item => UnitKey(item.Block?.Identifier, item.Unit.Identifier),
                item => item.Unit,
                StringComparer.OrdinalIgnoreCase);
            var unitsCreated = 0;
            foreach (var row in preview.Units.Where(item => !item.Existing))
            {
                var unit = new Unit(
                    condominiumId,
                    row.Unit,
                    row.Block is null ? null : blocks[row.Block].Id,
                    row.Floor,
                    row.Description);
                db.Units.Add(unit);
                units[UnitKey(row.Block, row.Unit)] = unit;
                unitsCreated++;
            }
            await db.SaveChangesAsync(cancellationToken);

            var users = new Dictionary<string, ApplicationUser>(
                StringComparer.OrdinalIgnoreCase);
            var memberships = new Dictionary<Guid, CondominiumMembership>();
            var credentials = new List<SetupCredential>();
            var residentsLinked = 0;

            foreach (var row in preview.Residents)
            {
                if (!users.TryGetValue(row.Email, out var user))
                {
                    user = await userManager.FindByEmailAsync(row.Email);
                    if (user is null)
                    {
                        user = new ApplicationUser(
                            row.Name, row.Email, row.Phone);
                        user.RequirePasswordChange();
                        var password = GenerateTemporaryPassword();
                        var created = await userManager.CreateAsync(user, password);
                        if (!created.Succeeded)
                        {
                            return Results.BadRequest(new
                            {
                                error =
                                    "Não foi possível criar um dos usuários. "
                                    + "Revise os dados e tente novamente."
                            });
                        }
                        credentials.Add(new SetupCredential(
                            user.Id,
                            user.FullName,
                            user.Email!,
                            password));
                    }
                    users[row.Email] = user;
                }

                if (!memberships.TryGetValue(user.Id, out var membership))
                {
                    membership = await db.CondominiumMemberships
                        .SingleOrDefaultAsync(item =>
                            item.UserId == user.Id
                            && item.CondominiumId == condominiumId,
                            cancellationToken)
                        ?? new CondominiumMembership(user.Id, condominiumId);
                    if (db.Entry(membership).State == EntityState.Detached)
                        db.CondominiumMemberships.Add(membership);
                    memberships[user.Id] = membership;

                    var role = await db.CondominiumMembershipRoles
                        .SingleOrDefaultAsync(item =>
                            item.CondominiumMembershipId == membership.Id
                            && item.Role == CondominiumRole.Resident,
                            cancellationToken);
                    if (role is null)
                    {
                        db.CondominiumMembershipRoles.Add(
                            new CondominiumMembershipRole(
                                membership.Id,
                                CondominiumRole.Resident));
                    }
                }

                if (row.Unit is not null)
                {
                    var unit = units[UnitKey(row.Block, row.Unit)];
                    var relationship = ParseRelationship(row.Relationship)!;
                    var unitMembership = await db.UnitMemberships
                        .SingleOrDefaultAsync(item =>
                            item.UserId == user.Id
                            && item.UnitId == unit.Id
                            && item.RelationshipType == relationship.Value,
                            cancellationToken);
                    if (unitMembership is null)
                    {
                        db.UnitMemberships.Add(new UnitMembership(
                            user.Id,
                            unit.Id,
                            relationship.Value,
                            row.Resident,
                            row.PrimaryResidence));
                    }
                    else if (!unitMembership.IsActive)
                    {
                        unitMembership.Reactivate(
                            row.Resident,
                            row.PrimaryResidence,
                            DateTime.UtcNow);
                    }
                    else
                    {
                        unitMembership.Update(
                            relationship.Value,
                            row.Resident,
                            row.PrimaryResidence);
                    }
                }
                residentsLinked++;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new SetupConfirmationResponse(
                blocksCreated,
                unitsCreated,
                residentsLinked,
                credentials,
                "Configuração concluída com sucesso."));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new
            {
                error =
                    "Os dados foram alterados por outra operação. "
                    + "Gere a prévia novamente; nada foi importado."
            });
        }
    }

    private static async Task<SetupPreviewResponse> BuildPreviewAsync(
        Guid condominiumId,
        SetupRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var sourceUnits = request.Units ?? [];
        var sourceResidents = request.Residents ?? [];
        var errors = new List<SetupIssue>();
        var warnings = new List<SetupIssue>();

        var existingBlocks = await db.CondominiumBlocks.AsNoTracking()
            .Where(item => item.CondominiumId == condominiumId)
            .ToDictionaryAsync(
                item => item.Identifier,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        var existingUnitRows = await (
                from unit in db.Units.AsNoTracking()
                join block in db.CondominiumBlocks.AsNoTracking()
                    on unit.BlockId equals block.Id into unitBlocks
                from block in unitBlocks.DefaultIfEmpty()
                where unit.CondominiumId == condominiumId
                select new
                {
                    unit.Identifier,
                    Block = block == null ? null : block.Identifier
                })
            .ToListAsync(cancellationToken);
        var existingUnits = existingUnitRows.ToDictionary(
            item => UnitKey(item.Block, item.Identifier),
            StringComparer.OrdinalIgnoreCase);

        var normalizedUnits = sourceUnits.Select(row => new SetupUnitRow(
            row.Line,
            Optional(row.Block),
            Optional(row.Unit),
            Optional(row.Floor),
            Optional(row.Description))).ToArray();
        var blockNames = normalizedUnits
            .Select(item => item.Block)
            .Concat(sourceResidents.Select(item => Optional(item.Block)))
            .Where(item => item is not null)
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasBlocks = existingBlocks.Count > 0 || blockNames.Length > 0;
        var unitKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unitPreviews = new List<SetupUnitPreview>();

        foreach (var row in normalizedUnits)
        {
            if (request.NoRegistrableUnits)
            {
                errors.Add(new SetupIssue(
                    row.Line, "Unit",
                    "Remova as unidades ou desmarque a opção de condomínio sem unidades."));
                continue;
            }
            ValidateLength(row.Block, 50, row.Line, "Block", errors);
            ValidateLength(row.Unit, 50, row.Line, "Unit", errors);
            ValidateLength(row.Floor, 20, row.Line, "Floor", errors);
            ValidateLength(row.Description, 500, row.Line, "Description", errors);
            if (row.Unit is null)
            {
                errors.Add(new SetupIssue(
                    row.Line, "Unit", "A identificação da unidade é obrigatória."));
                continue;
            }
            if (hasBlocks && row.Block is null)
            {
                errors.Add(new SetupIssue(
                    row.Line, "Block",
                    "Informe o bloco porque este condomínio utiliza blocos."));
            }

            var key = UnitKey(row.Block, row.Unit);
            if (!unitKeys.Add(key))
            {
                errors.Add(new SetupIssue(
                    row.Line, "Unit",
                    "Unidade duplicada no lote para o mesmo bloco."));
                continue;
            }
            var existing = existingUnits.ContainsKey(key);
            if (existing)
            {
                warnings.Add(new SetupIssue(
                    row.Line, "Unit",
                    "A unidade já existe e será reutilizada."));
            }
            unitPreviews.Add(new SetupUnitPreview(
                row.Line,
                row.Block,
                row.Unit,
                row.Floor,
                row.Description,
                existing));
        }

        var availableUnits = new HashSet<string>(
            existingUnits.Keys,
            StringComparer.OrdinalIgnoreCase);
        availableUnits.UnionWith(unitPreviews.Select(
            item => UnitKey(item.Block, item.Unit)));
        var emails = sourceResidents
            .Select(item => Optional(item.Email)?.ToLowerInvariant())
            .Where(item => item is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingUsers = await db.Users.AsNoTracking()
            .Where(item => item.Email != null && emails.Contains(item.Email))
            .ToDictionaryAsync(
                item => item.Email!,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        var existingUserIds = existingUsers.Values
            .Select(item => item.Id)
            .ToArray();
        var existingMemberships = await db.CondominiumMemberships
            .AsNoTracking()
            .Where(item =>
                item.CondominiumId == condominiumId
                && existingUserIds.Contains(item.UserId))
            .ToDictionaryAsync(
                item => item.UserId,
                cancellationToken);
        var existingMembershipIds = existingMemberships.Values
            .Select(item => item.Id)
            .ToArray();
        var residentRoles = await db.CondominiumMembershipRoles
            .AsNoTracking()
            .Where(item =>
                existingMembershipIds.Contains(
                    item.CondominiumMembershipId)
                && item.Role == CondominiumRole.Resident)
            .ToDictionaryAsync(
                item => item.CondominiumMembershipId,
                cancellationToken);
        var residentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var residentPreviews = new List<SetupResidentPreview>();

        foreach (var source in sourceResidents)
        {
            var block = Optional(source.Block);
            var unit = Optional(source.Unit);
            var name = Optional(source.Name);
            var email = Optional(source.Email)?.ToLowerInvariant();
            var phone = Optional(source.Phone);
            var relationship = ParseRelationship(source.Relationship);
            var residentValid = TryBoolean(
                source.Resident, false, out var resident);
            var primaryValid = TryBoolean(
                source.PrimaryResidence, false, out var primary);

            ValidateLength(block, 50, source.Line, "Block", errors);
            ValidateLength(unit, 50, source.Line, "Unit", errors);
            ValidateLength(name, 200, source.Line, "Name", errors);
            ValidateLength(email, 254, source.Line, "Email", errors);
            ValidateLength(phone, 30, source.Line, "Phone", errors);
            if (name is null)
                errors.Add(new SetupIssue(
                    source.Line, "Name", "O nome é obrigatório."));
            if (email is null
                || !new EmailAddressAttribute().IsValid(email))
                errors.Add(new SetupIssue(
                    source.Line, "Email", "Informe um e-mail válido."));
            if (!residentValid)
                errors.Add(new SetupIssue(
                    source.Line, "Resident",
                    "Use Yes/No, Sim/Não, True/False ou 1/0."));
            if (!primaryValid)
                errors.Add(new SetupIssue(
                    source.Line, "PrimaryResidence",
                    "Use Yes/No, Sim/Não, True/False ou 1/0."));
            if (primary && !resident)
                errors.Add(new SetupIssue(
                    source.Line, "PrimaryResidence",
                    "Residência principal exige Resident = Yes."));

            if (unit is null)
            {
                if (block is not null)
                    errors.Add(new SetupIssue(
                        source.Line, "Block",
                        "Block só pode ser informado junto com Unit."));
                if (Optional(source.Relationship) is not null
                    || resident || primary)
                    errors.Add(new SetupIssue(
                        source.Line, "Unit",
                        "Relacionamento e indicadores de residência exigem uma unidade."));
            }
            else
            {
                if (request.NoRegistrableUnits)
                    errors.Add(new SetupIssue(
                        source.Line, "Unit",
                        "Este condomínio foi marcado como sem unidades cadastráveis."));
                if (hasBlocks && block is null)
                    errors.Add(new SetupIssue(
                        source.Line, "Block",
                        "Informe o bloco da unidade referenciada."));
                if (relationship is null)
                    errors.Add(new SetupIssue(
                        source.Line, "Relationship",
                        "Use Owner, Tenant ou AuthorizedOccupant."));
                if (!availableUnits.Contains(UnitKey(block, unit)))
                    errors.Add(new SetupIssue(
                        source.Line, "Unit",
                        "A unidade referenciada não existe no condomínio nem neste lote."));
            }

            var duplicateKey =
                $"{email}\u001f{UnitKey(block, unit ?? string.Empty)}"
                + $"\u001f{relationship}";
            if (!residentKeys.Add(duplicateKey))
                errors.Add(new SetupIssue(
                    source.Line, "Email",
                    "Vínculo de morador duplicado no lote."));

            ApplicationUser? existingApplicationUser = null;
            var existingUser = email is not null
                && existingUsers.TryGetValue(
                    email, out existingApplicationUser);
            if (existingUser && !existingApplicationUser!.IsActive)
                errors.Add(new SetupIssue(
                    source.Line, "Email",
                    "O usuário existente está inativo."));
            else if (existingUser)
            {
                warnings.Add(new SetupIssue(
                    source.Line, "Email",
                    "O usuário existente será reutilizado; uma nova senha não será gerada."));
                if (existingMemberships.TryGetValue(
                        existingApplicationUser!.Id,
                        out var existingMembership)
                    && (!existingMembership.IsActive
                        || existingMembership.EndedAt is not null))
                {
                    errors.Add(new SetupIssue(
                        source.Line, "Email",
                        "O vínculo existente com o condomínio está inativo."));
                }
                else if (existingMembership is not null
                    && residentRoles.TryGetValue(
                        existingMembership.Id,
                        out var residentRole)
                    && (!residentRole.IsActive
                        || residentRole.RevokedAt is not null))
                {
                    errors.Add(new SetupIssue(
                        source.Line, "Email",
                        "O papel de morador existente está inativo."));
                }
            }

            if (name is not null && email is not null)
            {
                residentPreviews.Add(new SetupResidentPreview(
                    source.Line,
                    block,
                    unit,
                    name,
                    email,
                    phone,
                    relationship?.ToString(),
                    resident,
                    primary,
                    existingUser));
            }
        }

        if (request.NoRegistrableUnits)
        {
            warnings.Add(new SetupIssue(
                0, "Units",
                "O condomínio será mantido sem unidades cadastráveis."));
        }

        var draft = new SetupRequest(
            request.NoRegistrableUnits,
            normalizedUnits,
            sourceResidents);
        var blocks = blockNames.Select(identifier =>
            new SetupBlockPreview(
                identifier,
                existingBlocks.ContainsKey(identifier))).ToArray();
        return new SetupPreviewResponse(
            draft,
            blocks,
            unitPreviews,
            residentPreviews,
            warnings,
            errors,
            new SetupTotals(
                blocks.Length,
                unitPreviews.Count,
                residentPreviews.Count,
                residentPreviews.Count(item => item.ExistingUser),
                residentPreviews
                    .Where(item => !item.ExistingUser)
                    .Select(item => item.Email)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()));
    }

    private static async Task<IResult?> CheckAccessAsync(
        Guid condominiumId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var claim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var userId))
            return Results.Unauthorized();
        var userActive = await db.Users.AsNoTracking()
            .AnyAsync(item => item.Id == userId && item.IsActive,
                cancellationToken);
        if (!userActive) return Results.Unauthorized();
        var condominium = await db.Condominiums.AsNoTracking()
            .Where(item => item.Id == condominiumId)
            .Select(item => new { item.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (condominium is null)
            return Results.NotFound(new { error = "Condomínio não encontrado." });
        if (!condominium.IsActive)
            return Results.Conflict(new
            {
                error = "Condomínio inativo não pode ser configurado."
            });
        if (principal.IsInRole(DependencyInjection.PlatformAdminRole))
            return null;

        var manager = await db.CondominiumMemberships.AsNoTracking()
            .Where(item =>
                item.UserId == userId
                && item.CondominiumId == condominiumId
                && item.IsActive
                && item.EndedAt == null)
            .Join(
                db.CondominiumMembershipRoles.AsNoTracking().Where(role =>
                    role.Role == CondominiumRole.Manager
                    && role.IsActive
                    && role.RevokedAt == null),
                membership => membership.Id,
                role => role.CondominiumMembershipId,
                (_, _) => true)
            .AnyAsync(cancellationToken);
        return manager ? null : Results.Forbid();
    }

    private static string UnitKey(string? block, string unit) =>
        $"{Optional(block)?.ToUpperInvariant() ?? "<NO-BLOCK>"}"
        + $"\u001f{unit.Trim().ToUpperInvariant()}";

    private static string? Optional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static void ValidateLength(
        string? value,
        int maximum,
        int line,
        string column,
        ICollection<SetupIssue> errors)
    {
        if (value?.Length > maximum)
            errors.Add(new SetupIssue(
                line, column,
                $"O valor deve possuir no máximo {maximum} caracteres."));
    }

    private static UnitRelationshipType? ParseRelationship(string? value)
    {
        var normalized = Optional(value)?.ToLowerInvariant()
            .Replace("á", "a")
            .Replace("ó", "o");
        return normalized switch
        {
            "owner" or "proprietario" => UnitRelationshipType.Owner,
            "tenant" or "inquilino" => UnitRelationshipType.Tenant,
            "authorizedoccupant" or "authorized occupant"
                or "morador autorizado" => UnitRelationshipType.AuthorizedOccupant,
            _ => null
        };
    }

    private static bool TryBoolean(
        string? value,
        bool defaultValue,
        out bool result)
    {
        var normalized = Optional(value)?.ToLowerInvariant();
        if (normalized is null)
        {
            result = defaultValue;
            return true;
        }
        if (normalized is "yes" or "sim" or "true" or "1")
        {
            result = true;
            return true;
        }
        if (normalized is "no" or "nao" or "não" or "false" or "0")
        {
            result = false;
            return true;
        }
        result = defaultValue;
        return false;
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;
        var characters = new char[14];
        characters[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        characters[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        characters[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var index = 3; index < characters.Length; index++)
        {
            characters[index] =
                all[RandomNumberGenerator.GetInt32(all.Length)];
        }
        for (var index = characters.Length - 1; index > 0; index--)
        {
            var replacement = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[replacement]) =
                (characters[replacement], characters[index]);
        }
        return new string(characters);
    }

    internal static GeneratorResult GenerateUnits(
        IReadOnlyList<SetupGeneratorTower> towers)
    {
        var units = new List<SetupUnitRow>();
        var errors = new List<SetupIssue>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var line = 1;
        for (var towerIndex = 0; towerIndex < towers.Count; towerIndex++)
        {
            var tower = towers[towerIndex];
            var block = Optional(tower.Name);
            if (towers.Count > 1 && block is null)
            {
                errors.Add(new SetupIssue(
                    towerIndex + 1,
                    "Tower",
                    "Informe o nome de cada torre quando houver mais de uma."));
            }
            foreach (var segment in tower.Segments ?? [])
            {
                if (segment.StartFloor > segment.EndFloor)
                {
                    errors.Add(new SetupIssue(
                        line, "Floor",
                        "O andar inicial não pode ser maior que o andar final."));
                    continue;
                }
                if (segment.UnitsPerFloor is < 1 or > 100)
                {
                    errors.Add(new SetupIssue(
                        line, "UnitsPerFloor",
                        "Informe de 1 a 100 unidades por andar."));
                    continue;
                }
                if (segment.Digits is < 1 or > 10)
                {
                    errors.Add(new SetupIssue(
                        line, "Digits", "Informe de 1 a 10 dígitos."));
                    continue;
                }
                var floorCount =
                    (long)segment.EndFloor - segment.StartFloor + 1;
                if (floorCount * segment.UnitsPerFloor
                    + units.Count > SetupSpreadsheetReader.MaximumRows)
                {
                    errors.Add(new SetupIssue(
                        line,
                        "UnitsPerFloor",
                        $"O gerador aceita no máximo "
                        + $"{SetupSpreadsheetReader.MaximumRows} unidades por lote."));
                    continue;
                }
                var sequential = segment.FirstUnit;
                for (var floor = segment.StartFloor;
                     floor <= segment.EndFloor;
                     floor++)
                {
                    for (var position = 0;
                         position < segment.UnitsPerFloor;
                         position++)
                    {
                        var number = segment.IncludeFloorNumber
                            ? (segment.FirstUnit + position).ToString()
                                .PadLeft(segment.Digits, '0')
                            : (sequential++).ToString()
                                .PadLeft(segment.Digits, '0');
                        var identifier = Optional(segment.Prefix)
                            + (segment.IncludeFloorNumber
                                ? floor.ToString()
                                : string.Empty)
                            + number
                            + Optional(segment.Suffix);
                        var key = UnitKey(block, identifier);
                        if (!keys.Add(key))
                        {
                            errors.Add(new SetupIssue(
                                line, "Unit",
                                $"A configuração gera a unidade duplicada {identifier}."));
                        }
                        else
                        {
                            units.Add(new SetupUnitRow(
                                line,
                                block,
                                identifier,
                                floor == 0 ? "Ground" : floor.ToString(),
                                null));
                        }
                        line++;
                    }
                }
            }
        }
        return new GeneratorResult(units, errors);
    }

    internal sealed record GeneratorResult(
        IReadOnlyList<SetupUnitRow> Units,
        IReadOnlyList<SetupIssue> Errors);
}
