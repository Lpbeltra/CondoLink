using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using CondoLink.Api.Features.Management;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.CondominiumMembers;

public sealed class AssistantResidentRegistrationService(AppDbContext db, ResidentOnboardingService onboarding)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    public async Task<AssistantPrepareResult> PrepareAsync(AssistantResidentRegistrationInput input, CancellationToken ct)
    {
        var missing = Missing(input); if (missing.Count != 0) return AssistantPrepareResult.Missing(missing);
        if (!await IsAuthorizedAsync(input.ActorUserId, input.CondominiumId, ct)) return AssistantPrepareResult.Forbidden();
        var unitIdentifier = Regex.Replace(input.UnitIdentifier!.Trim(),
            @"^(?:unidade|apartamento|apto\.?)\s+", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        var blockIdentifier = string.IsNullOrWhiteSpace(input.BlockIdentifier) ? null
            : Regex.Replace(input.BlockIdentifier.Trim(), @"^bloco\s+", "",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        if (!string.IsNullOrWhiteSpace(blockIdentifier))
            unitIdentifier = Regex.Replace(unitIdentifier,
                $@"\s*,?\s*(?:do\s+)?bloco\s+{Regex.Escape(blockIdentifier)}\s*$", "",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        var unit = await ResolveUnitAsync(input.CondominiumId, unitIdentifier, blockIdentifier, ct);
        if (unit.Count == 0) return AssistantPrepareResult.UnitNotFound();
        if (unit.Count > 1) return AssistantPrepareResult.Ambiguous(unit.Select(x => x.Display).ToArray());
        var email = input.Email!.Trim().ToLowerInvariant();
        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)) return AssistantPrepareResult.Invalid("Email is invalid.");
        var phone = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : Domain.PhoneNumberNormalizer.Normalize(input.PhoneNumber.Trim());
        if (input.PhoneNumber is not null && phone is null) return AssistantPrepareResult.Invalid("PhoneNumber must be valid; include + and the country code outside Brazil.");
        if (!Enum.TryParse<UnitRelationshipType>(input.RelationshipType, true, out var relationship) || !Enum.IsDefined(relationship)) return AssistantPrepareResult.Invalid("Relationship type must be Owner, Tenant or AuthorizedOccupant.");
        var payload = new ResidentRegistrationPayload(input.FullName!.Trim(), email, phone, unit[0].Id, relationship.ToString(), true, input.IsPrimaryResidence, input.FirstAccessChannel ?? "None", input.EmailDeliveryEnabled, input.SendAccessEmail, input.InvitationOperationId);
        var now = DateTime.UtcNow;
        foreach (var old in await db.PendingAssistantActions.Where(x => x.ActorUserId == input.ActorUserId && x.CondominiumId == input.CondominiumId && x.Channel == input.Channel && x.ExternalContextId == input.ExternalContextId && x.ActionType == PendingAssistantActionType.ResidentRegistration && x.Status == PendingAssistantActionStatus.Pending).ToListAsync(ct)) old.TryCancel();
        var action = new PendingAssistantAction(PendingAssistantActionType.ResidentRegistration, input.ActorUserId, input.CondominiumId, input.Channel, input.ExternalContextId, JsonSerializer.Serialize(payload), input.IdempotencyKey, now, now + Ttl);
        db.PendingAssistantActions.Add(action); await db.SaveChangesAsync(ct);
        return AssistantPrepareResult.Ready(action.Id, new AssistantResidentPreview(payload.FullName, unit[0].Display, payload.Email, payload.PhoneNumber, payload.RelationshipType));
    }

    public async Task<AssistantActionExecutionResult> ExecuteAsync(Guid actionId, Guid actorUserId, Guid condominiumId, CancellationToken ct)
    {
        var action = await db.PendingAssistantActions.SingleOrDefaultAsync(x => x.Id == actionId && x.ActorUserId == actorUserId && x.CondominiumId == condominiumId, ct);
        if (action is null) return AssistantActionExecutionResult.NotFound();
        if (action.Status == PendingAssistantActionStatus.Executed) return AssistantActionExecutionResult.AlreadyExecuted();
        if (action.TryExpire(DateTime.UtcNow)) { await db.SaveChangesAsync(ct); return AssistantActionExecutionResult.Expired(); }
        if (!action.TryBeginExecution(DateTime.UtcNow)) return AssistantActionExecutionResult.Unavailable(action.Status);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return AssistantActionExecutionResult.Unavailable(action.Status); }
        if (!await IsAuthorizedAsync(actorUserId, condominiumId, ct)) return await FailAsync(action, "Forbidden", ct);
        var p = JsonSerializer.Deserialize<ResidentRegistrationPayload>(action.PayloadJson); if (p is null) return await FailAsync(action, "InvalidPayload", ct);
        var result = await onboarding.OnboardAsync(actorUserId, condominiumId, new ResidentOnboardingInput(p.FullName,p.Email,p.PhoneNumber,p.UnitId,p.RelationshipType,p.IsResident,p.IsPrimaryResidence,p.FirstAccessChannel,p.EmailDeliveryEnabled,p.SendAccessEmail,p.InvitationOperationId), ct);
        if (!result.Succeeded) return await FailAsync(action, result.Error.ToString(), ct);
        action.Complete(DateTime.UtcNow, JsonSerializer.Serialize(new { UserId = result.User!.Id, MembershipId = result.Membership!.Id })); await db.SaveChangesAsync(ct); return AssistantActionExecutionResult.Executed();
    }
    public async Task<bool> CancelAsync(Guid id, Guid actor, Guid condo, CancellationToken ct) { var a=await db.PendingAssistantActions.SingleOrDefaultAsync(x=>x.Id==id&&x.ActorUserId==actor&&x.CondominiumId==condo,ct); if(a is null)return false; var changed=a.TryCancel(); if(!changed)return a.Status==PendingAssistantActionStatus.Cancelled; try { await db.SaveChangesAsync(ct); return true; } catch (DbUpdateConcurrencyException) { return false; } }
    private async Task<AssistantActionExecutionResult> FailAsync(PendingAssistantAction a,string code,CancellationToken ct){a.Fail(DateTime.UtcNow,JsonSerializer.Serialize(new{Code=code}));await db.SaveChangesAsync(ct);return AssistantActionExecutionResult.Failed(code);}
    private async Task<bool> IsAuthorizedAsync(Guid actor,Guid condo,CancellationToken ct) => await db.Users.AsNoTracking().AnyAsync(x=>x.Id==actor&&x.IsActive,ct) && await db.Condominiums.AsNoTracking().AnyAsync(x=>x.Id==condo&&x.IsActive,ct) && await SubManagerAccess.HasAsync(db,actor,condo,SubManagerModule.Management,ct);
    private async Task<List<UnitChoice>> ResolveUnitAsync(Guid condo,string unit,string? block,CancellationToken ct) => await (from u in db.Units.AsNoTracking() join b in db.CondominiumBlocks.AsNoTracking() on u.BlockId equals b.Id into bs from b in bs.DefaultIfEmpty() where u.CondominiumId==condo&&u.IsActive&&u.Identifier.Trim().ToLower()==unit.Trim().ToLower()&&(block==null||(b!=null&&b.Identifier.Trim().ToLower()==block.Trim().ToLower())) select new UnitChoice(u.Id,b==null?u.Identifier:$"Bloco {b.Identifier} — {u.Identifier}")).ToListAsync(ct);
    private static List<string> Missing(AssistantResidentRegistrationInput x) { var r=new List<string>();if(string.IsNullOrWhiteSpace(x.FullName))r.Add("FullName");if(string.IsNullOrWhiteSpace(x.Email))r.Add("Email");if(string.IsNullOrWhiteSpace(x.UnitIdentifier))r.Add("UnitIdentifier");if(string.IsNullOrWhiteSpace(x.RelationshipType))r.Add("RelationshipType");return r; }
    private sealed record UnitChoice(Guid Id,string Display);
}
public sealed record AssistantResidentRegistrationInput(Guid ActorUserId,Guid CondominiumId,CondominiumAssistantChannel Channel,string? ExternalContextId,string IdempotencyKey,string? FullName,string? Email,string? PhoneNumber,string? UnitIdentifier,string? BlockIdentifier,string? RelationshipType,bool IsPrimaryResidence,string? FirstAccessChannel=null,bool EmailDeliveryEnabled=false,bool SendAccessEmail=false,string? InvitationOperationId=null);
public sealed record ResidentRegistrationPayload(string FullName,string Email,string? PhoneNumber,Guid UnitId,string RelationshipType,bool IsResident,bool IsPrimaryResidence,string FirstAccessChannel,bool EmailDeliveryEnabled,bool SendAccessEmail,string? InvitationOperationId);
public sealed record AssistantResidentPreview(string FullName,string Unit,string Email,string? PhoneNumber,string RelationshipType);
public sealed record AssistantPrepareResult(bool ReadyToPreview,Guid? ActionId,AssistantResidentPreview? Preview,IReadOnlyList<string>? MissingFields,string? Error,IReadOnlyList<string>? UnitOptions){public static AssistantPrepareResult Missing(IReadOnlyList<string>x)=>new(false,null,null,x,null,null);public static AssistantPrepareResult Forbidden()=>new(false,null,null,null,"Forbidden",null);public static AssistantPrepareResult UnitNotFound()=>new(false,null,null,null,"UnitNotFound",null);public static AssistantPrepareResult Ambiguous(IReadOnlyList<string>x)=>new(false,null,null,null,"UnitAmbiguous",x);public static AssistantPrepareResult Invalid(string x)=>new(false,null,null,null,x,null);public static AssistantPrepareResult Ready(Guid x,AssistantResidentPreview p)=>new(true,x,p,null,null,null);}
public sealed record AssistantActionExecutionResult(string Status,string? Error=null){public static AssistantActionExecutionResult NotFound()=>new("NotFound");public static AssistantActionExecutionResult AlreadyExecuted()=>new("Executed");public static AssistantActionExecutionResult Expired()=>new("Expired");public static AssistantActionExecutionResult Unavailable(PendingAssistantActionStatus s)=>new(s.ToString());public static AssistantActionExecutionResult Executed()=>new("Executed");public static AssistantActionExecutionResult Failed(string e)=>new("Failed",e);}
