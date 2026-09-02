using System.ComponentModel.DataAnnotations;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using CondoLink.Domain.Entities;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Management;
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
                "condolink-moradores.xlsx",
                string.Empty),
            _ => (string.Empty, string.Empty)
        };
        if (fileName.Length == 0)
            return Results.NotFound(new { error = "Modelo não encontrado." });

        return template.Equals("residents", StringComparison.OrdinalIgnoreCase)
            ? Results.File(ResidentImportTemplate.Create(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName)
            : Results.File(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(content),
                "text/csv; charset=utf-8", fileName);
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
                residentsFile, ResidentHeaders, cancellationToken, ["SendAccessEmail", "FirstAccessChannel"]);
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
                row.Values["PrimaryResidence"],
                row.Values["SendAccessEmail"],
                row.Values["FirstAccessChannel"])));
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
        IServiceProvider services,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var access = await CheckAccessAsync(
            condominiumId, principal, db, cancellationToken);
        if (access is not null) return access;
        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
        try
        {
            var preview = await BuildPreviewAsync(
                condominiumId, request, db, cancellationToken);
            if (preview.Errors.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.BadRequest(preview);
            }
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
            var usersToInvite = new List<ApplicationUser>();
            var usersToInviteByWhatsApp = new List<ApplicationUser>();
            var usersToInviteByBoth = new List<ApplicationUser>();
            var residentsLinked = 0;
            var usersCreated = 0;
            var usersReused = 0;
            var membershipsCreated = 0;
            var membershipsExisting = 0;

            foreach (var row in preview.Residents)
            {
                var identityKey = row.ExistingUserId?.ToString() ?? row.Email;
                if (!users.TryGetValue(identityKey, out var user))
                {
                    user = row.ExistingUserId is Guid existingUserId
                        ? await db.Users.SingleOrDefaultAsync(
                            item => item.Id == existingUserId,
                            cancellationToken)
                        : await userManager.FindByEmailAsync(row.Email);
                    if (user is null)
                    {
                        user = new ApplicationUser(
                            row.Name, row.Email, row.Phone);
                        user.RequirePasswordChange();
                        user.SetEmailDeliveryEnabled(row.FirstAccessChannel is "Email" or "WhatsAppAndEmail");
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
                        AddInvitationTarget(row.FirstAccessChannel, user, usersToInvite,
                            usersToInviteByWhatsApp, usersToInviteByBoth);
                        usersCreated++;
                    }
                    else
                    {
                        usersReused++;
                        if (user.MustChangePassword)
                        {
                            user.SetEmailDeliveryEnabled(row.FirstAccessChannel is "Email" or "WhatsAppAndEmail");
                            AddInvitationTarget(row.FirstAccessChannel, user, usersToInvite,
                                usersToInviteByWhatsApp, usersToInviteByBoth);
                        }
                    }
                    users[identityKey] = user;
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
                        membershipsCreated++;
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
                        membershipsExisting++;
                    }
                }
                residentsLinked++;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var condominiumName = await db.Condominiums.AsNoTracking()
                .Where(x => x.Id == condominiumId).Select(x => x.Name)
                .SingleAsync(cancellationToken);
            var invitationsSent = 0;
            var invitationsQueued = 0;
            var emailFailures = 0;
            var whatsappFailures = 0;
            var firstAccessService = services.GetService<FirstAccessService>();
            if (firstAccessService is not null)
                foreach (var user in usersToInvite.DistinctBy(x => x.Id))
                    if (await firstAccessService.SendAsync(user, condominiumName, cancellationToken)) invitationsSent++;
                    else emailFailures++;
            var whatsappInvitations = services.GetService<FirstAccessWhatsAppInvitationService>();
            if (whatsappInvitations is not null)
                foreach (var user in usersToInviteByWhatsApp.DistinctBy(x => x.Id))
                    if (await whatsappInvitations.EnqueueAsync(user, condominiumId, condominiumName,
                            $"import:{condominiumId:N}", cancellationToken)) invitationsQueued++;
                    else whatsappFailures++;
            if (firstAccessService is not null && whatsappInvitations is not null)
                foreach (var user in usersToInviteByBoth.DistinctBy(x => x.Id))
                {
                    var operationId = $"import:{condominiumId:N}";
                    var combined = await whatsappInvitations.DeliverBothAsync(
                        user, condominiumId, condominiumName, operationId, cancellationToken);
                    if (combined.EmailSent)
                        invitationsSent++;
                    else if (!combined.AlreadyProcessed) emailFailures++;
                    if (combined.WhatsAppQueued) invitationsQueued++;
                    else whatsappFailures++;
                }
            return Results.Ok(new SetupConfirmationResponse(
                blocksCreated,
                unitsCreated,
                residentsLinked,
                [],
                "Configuração concluída com sucesso.",
                usersCreated,
                usersReused,
                membershipsCreated,
                membershipsExisting,
                Math.Max(0,
                    (request.Residents?.Count ?? 0) - preview.Residents.Count),
                preview.Warnings.Count,
                invitationsSent,
                Math.Max(0, usersCreated - invitationsSent - invitationsQueued),
                invitationsQueued,
                emailFailures,
                whatsappFailures));
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
                    unit.Id,
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
            .Where(item => item is not null)
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var resolvableBlocks = existingBlocks.Keys.Concat(blockNames)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
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
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedEmails = emails.Select(item => item.ToUpperInvariant())
            .ToArray();
        var phones = sourceResidents
            .Select(item => Domain.PhoneNumberNormalizer.Normalize(item.Phone))
            .Where(item => item is not null)
            .Select(item => item!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var existingUserRows = await db.Users.AsNoTracking()
            .Where(item =>
                (item.NormalizedEmail != null
                    && normalizedEmails.Contains(item.NormalizedEmail))
                || (item.Email != null && emails.Contains(item.Email))
                || (item.NormalizedPhoneNumber != null
                    && phones.Contains(item.NormalizedPhoneNumber)))
            .ToListAsync(cancellationToken);
        var existingUsersByEmail = existingUserRows
            .Where(item => item.Email is not null)
            .ToDictionary(
                item => item.Email!,
                StringComparer.OrdinalIgnoreCase);
        var existingUsersByPhone = existingUserRows
            .Where(item => item.NormalizedPhoneNumber is not null)
            .ToDictionary(
                item => item.NormalizedPhoneNumber!,
                StringComparer.Ordinal);
        var existingUserIds = existingUserRows.Select(item => item.Id).ToArray();
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
        var existingUnitMemberships = await (
                from membership in db.UnitMemberships.AsNoTracking()
                join unit in db.Units.AsNoTracking()
                    on membership.UnitId equals unit.Id
                where existingUserIds.Contains(membership.UserId)
                    && unit.CondominiumId == condominiumId
                select membership)
            .ToListAsync(cancellationToken);
        var residentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batchEmails = new Dictionary<string, (string Name, string? Phone)>(
            StringComparer.OrdinalIgnoreCase);
        var batchPhones = new Dictionary<string, string>(StringComparer.Ordinal);
        var residentPreviews = new List<SetupResidentPreview>();

        foreach (var source in sourceResidents)
        {
            var block = Optional(source.Block);
            var unit = Optional(source.Unit);
            var name = NormalizePersonName(source.Name);
            var email = Optional(source.Email)?.ToLowerInvariant();
            var phone = Optional(source.Phone);
            var normalizedPhone = Domain.PhoneNumberNormalizer.Normalize(phone);
            var relationship = ParseRelationship(source.Relationship);
            var residentValid = TryBoolean(
                source.Resident, false, out var resident);
            var primaryValid = TryBoolean(
                source.PrimaryResidence, false, out var primary);
            var sendAccessEmailValid = TryBoolean(
                source.SendAccessEmail, false, out var sendAccessEmail);
            var firstAccessValid = TryFirstAccessChannel(
                source.FirstAccessChannel, sendAccessEmail, out var firstAccessChannel);

            if (block is not null)
            {
                var resolvedBlock = ResolveBlock(block, resolvableBlocks, out var ambiguousBlock);
                if (ambiguousBlock)
                    errors.Add(new SetupIssue(source.Line, "Block",
                        $"O bloco \"{block}\" corresponde a mais de um bloco cadastrado. Use o identificador completo."));
                else if (resolvedBlock is not null)
                    block = resolvedBlock;
            }

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
            if (phone is not null && normalizedPhone is null)
                errors.Add(new SetupIssue(
                    source.Line,
                    "Phone",
                    "Telefone inválido. Para números fora do Brasil, informe + e o código do país."));
            if (!residentValid)
                errors.Add(new SetupIssue(
                    source.Line, "Resident",
                    "Informe Sim ou Não. Também são aceitos Yes/No, True/False e 1/0."));
            if (!primaryValid)
                errors.Add(new SetupIssue(
                    source.Line, "PrimaryResidence",
                    "Informe Sim ou Não. Também são aceitos Yes/No, True/False e 1/0."));
            if (!sendAccessEmailValid)
                errors.Add(new SetupIssue(
                    source.Line, "SendAccessEmail",
                    "Informe Sim ou Não. Também são aceitos Yes/No, True/False e 1/0."));
            if (primary && !resident)
                errors.Add(new SetupIssue(
                    source.Line, "PrimaryResidence",
                    "Residência principal exige Morador = Sim."));

            if (!firstAccessValid)
                errors.Add(new SetupIssue(source.Line, "FirstAccessChannel",
                    "Informe WhatsApp, E-mail ou Não."));
            if ((firstAccessChannel is "WhatsApp" or "WhatsAppAndEmail") && normalizedPhone is null)
                errors.Add(new SetupIssue(source.Line, "FirstAccessChannel",
                    "WhatsApp exige um telefone válido."));

            if (unit is null)
            {
                if (block is not null)
                    errors.Add(new SetupIssue(
                        source.Line, "Block",
                        "Bloco só pode ser informado junto com Unidade."));
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
                        $"Relacionamento \"{Optional(source.Relationship)}\" não foi reconhecido. Use Proprietário, Inquilino ou Morador autorizado."));
                if (!availableUnits.Contains(UnitKey(block, unit)))
                    errors.Add(new SetupIssue(
                        source.Line, "Unit",
                        $"Não encontrei a unidade {unit}{(block is null ? string.Empty : $" no {block}")}. A importação de moradores não cria unidades."));
            }

            if (email is not null && name is not null
                && batchEmails.TryGetValue(email, out var priorEmailData)
                && (!string.Equals(priorEmailData.Name, name,
                        StringComparison.OrdinalIgnoreCase)
                    || (priorEmailData.Phone is not null
                        && normalizedPhone is not null
                        && priorEmailData.Phone != normalizedPhone)))
            {
                errors.Add(new SetupIssue(
                    source.Line, "Email",
                    "Conflict: o mesmo e-mail possui dados pessoais divergentes no lote."));
            }
            else if (email is not null && name is not null)
            {
                batchEmails[email] = (
                    name,
                    batchEmails.TryGetValue(email, out var existingBatchData)
                        ? existingBatchData.Phone ?? normalizedPhone
                        : normalizedPhone);
            }
            if (normalizedPhone is not null && email is not null
                && batchPhones.TryGetValue(normalizedPhone, out var priorPhoneEmail)
                && !string.Equals(priorPhoneEmail, email,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new SetupIssue(
                    source.Line, "Phone",
                    "Conflict: o mesmo telefone está associado a e-mails diferentes no lote."));
            }
            else if (normalizedPhone is not null && email is not null)
            {
                batchPhones[normalizedPhone] = email;
            }

            var identityKey = normalizedPhone ?? email ?? $"line:{source.Line}";
            var duplicateKey =
                $"{identityKey}\u001f{UnitKey(block, unit ?? string.Empty)}"
                + $"\u001f{relationship}";
            if (!residentKeys.Add(duplicateKey))
            {
                warnings.Add(new SetupIssue(
                    source.Line, "Email",
                    "ExistingMembership: linha/vínculo duplicado no lote; a linha será ignorada."));
                continue;
            }

            existingUsersByEmail.TryGetValue(
                email ?? string.Empty, out var emailUser);
            ApplicationUser? phoneUser = null;
            if (normalizedPhone is not null)
                existingUsersByPhone.TryGetValue(normalizedPhone, out phoneUser);
            if (emailUser is not null && phoneUser is not null
                && emailUser.Id != phoneUser.Id)
            {
                errors.Add(new SetupIssue(
                    source.Line, "Phone",
                    "Conflict: o e-mail e o telefone pertencem a usuários diferentes."));
            }
            var existingApplicationUser = emailUser ?? phoneUser;
            var existingUser = existingApplicationUser is not null;
            if (existingApplicationUser is { MustChangePassword: false }
                && firstAccessChannel != "None")
                errors.Add(new SetupIssue(source.Line, "FirstAccessChannel",
                    "O usuário existente já concluiu o primeiro acesso."));
            if (existingApplicationUser is not null
                && (firstAccessChannel is "Email" or "WhatsAppAndEmail")
                && !existingApplicationUser.EmailDeliveryEnabled)
                errors.Add(new SetupIssue(source.Line, "FirstAccessChannel",
                    "O usuário existente não possui e-mail marcado como entregável."));
            if (existingApplicationUser is not null
                && (firstAccessChannel is "WhatsApp" or "WhatsAppAndEmail")
                && string.IsNullOrWhiteSpace(existingApplicationUser.NormalizedPhoneNumber))
                errors.Add(new SetupIssue(source.Line, "FirstAccessChannel",
                    "O usuário existente não possui telefone normalizado para WhatsApp."));
            if (phoneUser is not null && email is not null
                && !string.Equals(phoneUser.Email, email,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new SetupIssue(
                    source.Line, "Email",
                    "Conflict: o telefone pertence a um usuário com outro e-mail."));
            }
            if (emailUser?.NormalizedPhoneNumber is not null
                && normalizedPhone is not null
                && emailUser.NormalizedPhoneNumber != normalizedPhone)
            {
                errors.Add(new SetupIssue(
                    source.Line, "Phone",
                    "Conflict: o usuário encontrado por e-mail já possui outro telefone."));
            }
            if (existingUser && !existingApplicationUser!.IsActive)
                errors.Add(new SetupIssue(
                    source.Line, "Email",
                    "O usuário existente está inativo."));
            else if (existingUser)
            {
                warnings.Add(new SetupIssue(
                    source.Line, "Email",
                    "O usuário existente será reutilizado; uma nova senha não será gerada."));
                if (name is not null && !string.Equals(
                        existingApplicationUser.FullName.Trim(), name,
                        StringComparison.OrdinalIgnoreCase))
                    warnings.Add(new SetupIssue(
                        source.Line, "Name",
                        "Warning: o nome diverge do cadastro existente e não será sobrescrito."));
                if (normalizedPhone is not null
                    && existingApplicationUser.NormalizedPhoneNumber is null)
                    warnings.Add(new SetupIssue(
                        source.Line, "Phone",
                        "Warning: o telefone informado não será adicionado automaticamente ao usuário existente."));
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

            var status = existingUser ? "ExistingUser" : "Ready";
            if (existingApplicationUser is not null && unit is not null
                && existingUnits.TryGetValue(UnitKey(block, unit), out var unitRow))
            {
                var membershipsForUnit = existingUnitMemberships.Where(item =>
                    item.UserId == existingApplicationUser.Id
                    && item.UnitId == unitRow.Id).ToArray();
                var exactMembership = membershipsForUnit.FirstOrDefault(item =>
                    item.RelationshipType == relationship);
                if (membershipsForUnit.Any(item =>
                        item.RelationshipType != relationship))
                {
                    errors.Add(new SetupIssue(
                        source.Line, "Relationship",
                        "Conflict: já existe vínculo com outra relação nesta unidade."));
                    status = "Conflict";
                }
                else if (exactMembership is not null)
                {
                    if (!exactMembership.IsActive
                        || exactMembership.IsResident != resident
                        || exactMembership.IsPrimaryResidence != primary)
                    {
                        errors.Add(new SetupIssue(
                            source.Line, "Relationship",
                            "Conflict: o vínculo existente possui estado ou indicadores divergentes."));
                        status = "Conflict";
                    }
                    else
                    {
                        warnings.Add(new SetupIssue(
                            source.Line, "Relationship",
                            "ExistingMembership: o vínculo já existe e não será alterado."));
                        status = "ExistingMembership";
                    }
                }
                else if (existingUnitMemberships.Any(item =>
                             item.UserId == existingApplicationUser.Id
                             && item.UnitId != unitRow.Id
                             && item.IsActive))
                {
                    warnings.Add(new SetupIssue(
                        source.Line, "Unit",
                        "Warning: o usuário já possui vínculo com outra unidade deste condomínio; nenhum vínculo será encerrado."));
                    status = "Warning";
                }
            }

            if (errors.Any(item => item.Line == source.Line))
                status = errors.Any(item => item.Line == source.Line
                    && item.Reason.StartsWith(
                        "Conflict:", StringComparison.Ordinal))
                    ? "Conflict"
                    : "Invalid";
            else if (warnings.Any(item => item.Line == source.Line)
                     && status is "Ready" or "ExistingUser")
                status = "Warning";

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
                    existingUser,
                    firstAccessChannel is "Email" or "WhatsAppAndEmail",
                    firstAccessChannel,
                    status,
                    normalizedPhone,
                    existingApplicationUser?.Id));
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

        return await SubManagerAccess.HasAsync(db, userId, condominiumId, SubManagerModule.Management, cancellationToken)
            ? null : Results.Forbid();
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
        var normalized = NormalizeLookup(Optional(value));
        return normalized switch
        {
            "owner" or "proprietario" => UnitRelationshipType.Owner,
            "tenant" or "inquilino" => UnitRelationshipType.Tenant,
            "authorizedoccupant" or "authorized occupant"
                or "morador autorizado" or "morador" or "residente"
                or "autorizado" => UnitRelationshipType.AuthorizedOccupant,
            _ => null
        };
    }

    private static string? NormalizePersonName(string? value)
    {
        var trimmed = Optional(value);
        return trimmed is null ? null : string.Join(' ',
            trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? ResolveBlock(string input, IReadOnlyList<string> blocks, out bool ambiguous)
    {
        ambiguous = false;
        var exact = blocks.FirstOrDefault(block => string.Equals(block, input, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;
        var key = NormalizeLookup(input)?.Replace("bloco ", "").Replace("torre ", "");
        var matches = blocks.Where(block =>
            NormalizeLookup(block)?.Replace("bloco ", "").Replace("torre ", "") == key).ToArray();
        ambiguous = matches.Length > 1;
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string? NormalizeLookup(string? value)
    {
        if (value is null) return null;
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC);
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

    private static bool TryFirstAccessChannel(
        string? value, bool legacyEmail, out string channel)
    {
        var normalized = NormalizeLookup(Optional(value));
        if (normalized is null)
        {
            channel = legacyEmail ? "Email" : "None";
            return true;
        }
        channel = normalized switch
        {
            "whatsapp" => "WhatsApp",
            "email" or "e-mail" => "Email",
            "whatsapp + email" or "whatsapp+email" or "whatsapp e email"
                or "whatsapp + e-mail" or "whatsapp+e-mail" => "WhatsAppAndEmail",
            "nao" or "none" => "None",
            _ => string.Empty
        };
        return channel.Length > 0;
    }

    private static void AddInvitationTarget(string channel, ApplicationUser user,
        List<ApplicationUser> email, List<ApplicationUser> whatsapp,
        List<ApplicationUser> both)
    {
        if (channel == "Email") email.Add(user);
        else if (channel == "WhatsApp") whatsapp.Add(user);
        else if (channel == "WhatsAppAndEmail") both.Add(user);
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
