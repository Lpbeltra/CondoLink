using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Domain;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

internal static class OverwatchCondominiumCreationService
{
    public static async Task<CreationResult> CreateAsync(AppDbContext db, CondominiumRequest request,
        Guid? administratorId, Guid? integrationId, string? externalCondominiumId, CancellationToken ct)
    {
        var error = CondominiumValidation.Validate(request);
        if (error is not null) return new(null, null, error, false);
        if (integrationId.HasValue && (!administratorId.HasValue || string.IsNullOrWhiteSpace(externalCondominiumId) || externalCondominiumId.Trim().Length > 100))
            return new(null, null, "ID externo inválido.", false);
        var name = request.Name!.Trim();
        var cnpj = RegistrationData.Digits(request.Cnpj)!;
        if (await db.Condominiums.AnyAsync(x => x.Name == name, ct))
            return new(null, null, integrationId.HasValue ? "Já existe condomínio com este nome ou CNPJ." : "A condominium with this name already exists.", true);
        if (await db.Condominiums.AnyAsync(x => x.Cnpj == cnpj, ct))
            return new(null, null, integrationId.HasValue ? "Já existe condomínio com este nome ou CNPJ." : "A condominium with this CNPJ already exists.", true);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var condominium = new Condominium(name, request.Email, cnpj, request.Address,
            request.City, request.State, request.HasDoorman, request.IsRemoteDoorman, request.DoormanContact);
        condominium.ConfigureWhatsAppUpdates(request.WhatsAppUpdatesEnabled ?? true, null);
        if (administratorId.HasValue) condominium.SetManagementCompany(administratorId.Value);
        db.Condominiums.Add(condominium);
        CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
        if (administratorId.HasValue)
            db.CondominiumManagementCompanyLinks.Add(new CondominiumManagementCompanyLink(condominium.Id, administratorId.Value));
        ExternalCondominiumMapping? mapping = null;
        if (integrationId.HasValue)
        {
            mapping = new ExternalCondominiumMapping(integrationId.Value, condominium.Id, externalCondominiumId!);
            db.ExternalCondominiumMappings.Add(mapping);
        }
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(condominium, mapping, null, false);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            return new(null, null, integrationId.HasValue
                ? "Não foi possível criar o condomínio e vínculo; verifique duplicidade."
                : "A condominium with this name or CNPJ already exists.", true);
        }
    }

    internal sealed record CreationResult(Condominium? Condominium, ExternalCondominiumMapping? Mapping, string? Error, bool Conflict);
}
