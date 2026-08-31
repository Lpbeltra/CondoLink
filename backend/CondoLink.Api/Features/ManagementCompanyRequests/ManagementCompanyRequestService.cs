using System.Data;
using System.Security.Claims;
using CondoLink.Api.Common;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.ManagementCompanyRequests;

public sealed class ManagementCompanyRequestService(AppDbContext db, ManagementCompanyRequestAccessService access, LocalFileStorage storage, ManagementCompanyRequestIdentifierService identifiers)
{
    public ManagementCompanyRequestService(AppDbContext db, ManagementCompanyRequestAccessService access, LocalFileStorage storage)
        : this(db, access, storage, new ManagementCompanyRequestIdentifierService(db, TimeProvider.System)) { }

    public async Task<ManagementCompanyRequest> CreateFineAsync(ClaimsPrincipal p, CreateFineCommand c, CancellationToken ct, IReadOnlyList<IFormFile>? files = null)
    {
        var a = await access.RequireManagementAsync(p, c.CondominiumId, ct);
        return await Create(c.CondominiumId, c.CategoryId, a, ManagementCompanyRequestType.Fine, async r =>
        {
            if (!await db.Units.AnyAsync(u => u.Id == c.UnitId && u.CondominiumId == c.CondominiumId, ct))
                throw new ValidationAppException("A unidade não pertence ao condomínio selecionado.");
            db.ManagementCompanyFineRequests.Add(new(r.Id, c.UnitId, c.Nature, c.Description, c.OccurrenceDate, c.Value, c.ValueNotDefined));
        }, null, files, ManagementCompanyRequestAttachmentPurpose.Request, ct);
    }

    public async Task<ManagementCompanyRequest> CreatePaymentAsync(ClaimsPrincipal p, CreatePaymentCommand c, CancellationToken ct, IReadOnlyList<IFormFile>? files = null)
    {
        var a = await access.RequireManagementAsync(p, c.CondominiumId, ct);
        Guid? id = null; string? name = null; PixKeyType? pt = null; string? pk = null;
        if (c.DueDate is null) throw new ValidationAppException("A data de vencimento é obrigatória.");
        if (c.IsReimbursement)
        {
            if (c.BeneficiaryUserId is not Guid bid || !await access.HasManagementScopeAsync(bid, c.CondominiumId, ct))
                throw new ValidationAppException("O beneficiário deve pertencer à gestão deste condomínio.");
            if (!string.IsNullOrWhiteSpace(c.ThirdPartyIdentification) || c.ThirdPartyForm is not null || !string.IsNullOrWhiteSpace(c.ThirdPartyPixKey) || !string.IsNullOrWhiteSpace(c.ThirdPartyBank) || !string.IsNullOrWhiteSpace(c.ThirdPartyAgency) || !string.IsNullOrWhiteSpace(c.ThirdPartyAccount))
                throw new ValidationAppException("Reembolso não aceita dados de terceiro.");
            var b = await db.Users.AsNoTracking().Where(x => x.Id == bid && x.IsActive).Select(x => new { x.Id, x.FullName, x.PixKeyType, x.PixKey }).SingleOrDefaultAsync(ct);
            if (b is null || b.PixKeyType is null || string.IsNullOrWhiteSpace(b.PixKey))
                throw new ValidationAppException("O beneficiário selecionado não possui PIX cadastrado.");
            id = b.Id; name = b.FullName; pt = b.PixKeyType; pk = b.PixKey;
        }
        else
        {
            if (c.BeneficiaryUserId is not null)
                throw new ValidationAppException("Terceiro não aceita beneficiário de reembolso.");
            if (string.IsNullOrWhiteSpace(c.ThirdPartyIdentification))
                throw new ValidationAppException("A identificação do terceiro é obrigatória.");
            if (c.ThirdPartyForm is null)
                throw new ValidationAppException("A forma de pagamento é obrigatória.");
            if (c.ThirdPartyForm == ManagementCompanyPaymentThirdPartyForm.Pix)
            {
                if (string.IsNullOrWhiteSpace(c.ThirdPartyPixKey))
                    throw new ValidationAppException("A chave PIX é obrigatória.");
                if (!string.IsNullOrWhiteSpace(c.ThirdPartyBank) || !string.IsNullOrWhiteSpace(c.ThirdPartyAgency) || !string.IsNullOrWhiteSpace(c.ThirdPartyAccount))
                    throw new ValidationAppException("PIX não pode conter dados bancários.");
                if (files is { Count: > 0 })
                    throw new ValidationAppException("Arquivos não são permitidos para PIX.");
            }
            else if (c.ThirdPartyForm == ManagementCompanyPaymentThirdPartyForm.Boleto)
            {
                if (files is null || files.Count == 0)
                    throw new ValidationAppException("Anexe o boleto.");
                if (!string.IsNullOrWhiteSpace(c.ThirdPartyPixKey))
                    throw new ValidationAppException("Boleto não pode conter chave PIX.");
                if (!string.IsNullOrWhiteSpace(c.ThirdPartyBank) || !string.IsNullOrWhiteSpace(c.ThirdPartyAgency) || !string.IsNullOrWhiteSpace(c.ThirdPartyAccount))
                    throw new ValidationAppException("Boleto não pode conter dados bancários.");
            }
            else if (c.ThirdPartyForm == ManagementCompanyPaymentThirdPartyForm.DepositAccount)
            {
                if (string.IsNullOrWhiteSpace(c.ThirdPartyBank) || string.IsNullOrWhiteSpace(c.ThirdPartyAgency) || string.IsNullOrWhiteSpace(c.ThirdPartyAccount))
                    throw new ValidationAppException("Banco, agência e conta são obrigatórios.");
                if (!string.IsNullOrWhiteSpace(c.ThirdPartyPixKey) || files is { Count: > 0 })
                    throw new ValidationAppException("Conta para depósito não aceita PIX nem boleto.");
            }
            else throw new ValidationAppException("A forma de pagamento é inválida.");
        }

        return await Create(c.CondominiumId, c.CategoryId, a, ManagementCompanyRequestType.Payment, r =>
        {
            db.ManagementCompanyPaymentRequests.Add(new(r.Id, c.Nature, c.Value, c.EventDate, c.DueDate, c.IsReimbursement, c.Notes, id, name, pt, pk, c.ThirdPartyIdentification, c.ThirdPartyForm, c.ThirdPartyPixKey, c.ThirdPartyBank, c.ThirdPartyAgency, c.ThirdPartyAccount));
            return Task.CompletedTask;
        }, null, files, c.IsReimbursement ? ManagementCompanyRequestAttachmentPurpose.Request : c.ThirdPartyForm == ManagementCompanyPaymentThirdPartyForm.Boleto ? ManagementCompanyRequestAttachmentPurpose.PaymentBoleto : ManagementCompanyRequestAttachmentPurpose.Request, ct);
    }

    public async Task<ManagementCompanyRequest> CreateQuestionAsync(ClaimsPrincipal p, CreateQuestionCommand c, CancellationToken ct, IReadOnlyList<IFormFile>? files = null)
    {
        var a = await access.RequireManagementAsync(p, c.CondominiumId, ct);
        if (string.IsNullOrWhiteSpace(c.Message) || c.Message.Trim().Length > 2000)
            throw new ValidationAppException("A mensagem é obrigatória e deve possuir no máximo 2000 caracteres.");
        return await Create(c.CondominiumId, c.CategoryId, a, ManagementCompanyRequestType.GeneralQuestion, r =>
        {
            db.ManagementCompanyGeneralQuestionRequests.Add(new(r.Id, c.Theme));
            return Task.CompletedTask;
        }, c.Message, files, ManagementCompanyRequestAttachmentPurpose.Request, ct);
    }

    private async Task<ManagementCompanyRequest> Create(Guid condo, Guid category, ManagementCompanyRequestActor actor, ManagementCompanyRequestType type, Func<ManagementCompanyRequest, Task> add, string? initial, IReadOnlyList<IFormFile>? files, ManagementCompanyRequestAttachmentPurpose filePurpose, CancellationToken ct)
    {
        Validate(files, 0);
        var saved = new List<string>();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({condo.ToString()}, 7311));", ct);
            var link = await db.CondominiumManagementCompanyLinks.AsNoTracking().SingleOrDefaultAsync(x => x.CondominiumId == condo && x.IsActive, ct);
            if (link is null) throw new ConflictAppException("Este condomínio não possui administradora ativa.");
            var form = type switch
            {
                ManagementCompanyRequestType.Fine => ManagementCompanyRequestFormType.UnitFine,
                ManagementCompanyRequestType.Payment => ManagementCompanyRequestFormType.SupplierPayment,
                _ => ManagementCompanyRequestFormType.Generic
            };
            var cat = await db.ManagementCompanyRequestCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == category && x.ManagementCompanyId == link.ManagementCompanyId && x.IsActive, ct);
            if (cat is null || cat.FormType != form) throw new ValidationAppException("A categoria não corresponde ao tipo de solicitação ou está inativa.");
            var responsible = await (from r in db.ManagementCompanyRequestCategoryResponsibles.AsNoTracking()
                                     join e in db.ManagementCompanyEmployees.AsNoTracking() on r.ManagementCompanyEmployeeId equals e.Id
                                     join u in db.Users.AsNoTracking() on e.UserId equals u.Id
                                     where r.ManagementCompanyRequestCategoryId == category && e.ManagementCompanyId == link.ManagementCompanyId && e.IsActive && u.IsActive
                                     select r.Id).AnyAsync(ct);
            if (!responsible) throw new ConflictAppException("Esta categoria ainda não está disponível para este condomínio. Entre em contato com sua administradora.");
            var (fid, created) = await identifiers.NextAsync(ct);
            var request = new ManagementCompanyRequest(condo, link.ManagementCompanyId, category, actor.UserId, type, fid, created);
            db.ManagementCompanyRequests.Add(request);
            try
            {
                await add(request);
                if (initial is not null)
                    db.ManagementCompanyRequestMessages.Add(new(request.Id, actor.UserId, initial));
            }
            catch (ArgumentException e)
            {
                throw new ValidationAppException(e.Message);
            }
            db.ManagementCompanyRequestHistories.Add(new(request.Id, ManagementCompanyRequestEventType.Created, null, request.Status, actor.UserId, null, request.CreatedAt));
            await SaveFiles(request.Id, null, actor.UserId, files, saved, filePurpose, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return request;
        }
        catch
        {
            foreach (var key in saved) storage.Delete(key);
            throw;
        }
    }

    public async Task AcknowledgeAsync(ManagementCompanyRequest r, ManagementCompanyRequestActor a, CancellationToken ct)
    {
        if (a.Kind != ManagementCompanyRequestActorKind.ManagementCompany || r.Status != ManagementCompanyRequestStatus.Submitted) return;
        var old = r.Status; var now = DateTime.UtcNow;
        r.Acknowledge(a.UserId, now);
        db.ManagementCompanyRequestHistories.Add(new(r.Id, ManagementCompanyRequestEventType.Acknowledged, old, r.Status, a.UserId, null, now));
        // A losing concurrent acknowledgement can surface as either the concurrency-token
        // mismatch (DbUpdateConcurrencyException) or, under real Postgres load, the raw
        // unique-index violation on the first-Acknowledged history row (DbUpdateException) —
        // both mean "someone else already acknowledged this", so both are swallowed here.
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { db.ChangeTracker.Clear(); }
    }

    public async Task StartProcessingAsync(ManagementCompanyRequest r, ManagementCompanyRequestActor a, CancellationToken ct)
    {
        if (a.Kind != ManagementCompanyRequestActorKind.ManagementCompany) throw new ForbiddenAppException("Somente a administradora pode iniciar o processamento.");
        if (r.Status == ManagementCompanyRequestStatus.InProgress) return;
        if (r.Status != ManagementCompanyRequestStatus.Submitted) throw new ConflictAppException("A solicitação já foi atualizada. Recarregue os dados.");
        var now = DateTime.UtcNow; var submitted = r.Status;
        r.Acknowledge(a.UserId, now);
        db.ManagementCompanyRequestHistories.Add(new(r.Id, ManagementCompanyRequestEventType.Acknowledged, submitted, r.Status, a.UserId, null, now));
        var acknowledged = r.Status; r.TransitionTo(ManagementCompanyRequestStatus.InProgress, now);
        db.ManagementCompanyRequestHistories.Add(new(r.Id, ManagementCompanyRequestEventType.StatusChanged, acknowledged, r.Status, a.UserId, null, now));
        try { await SaveConcurrency(ct); }
        catch (ConflictAppException)
        {
            db.ChangeTracker.Clear();
            if (await db.ManagementCompanyRequests.AsNoTracking().AnyAsync(x => x.Id == r.Id && x.Status == ManagementCompanyRequestStatus.InProgress, ct)) return;
            throw;
        }
    }

    public async Task<ManagementCompanyRequestHistory> TransitionAsync(ManagementCompanyRequest r, ManagementCompanyRequestActor a, ManagementCompanyRequestStatus next, string? reason, CancellationToken ct)
    {
        if (a.Kind != ManagementCompanyRequestActorKind.ManagementCompany) throw new ForbiddenAppException("Somente a administradora pode alterar o processamento.");
        if (next is ManagementCompanyRequestStatus.Submitted or ManagementCompanyRequestStatus.Acknowledged or ManagementCompanyRequestStatus.WaitingManager or ManagementCompanyRequestStatus.Cancelled)
            throw new ValidationAppException("Transição inválida.");
        var old = r.Status; var now = DateTime.UtcNow;
        try
        {
            if (next == ManagementCompanyRequestStatus.Completed) r.Complete(a.UserId, now);
            else r.TransitionTo(next, now);
        }
        catch (InvalidOperationException e)
        {
            throw new ConflictAppException(e.Message);
        }
        var history = new ManagementCompanyRequestHistory(r.Id, next == ManagementCompanyRequestStatus.Completed ? ManagementCompanyRequestEventType.Completed : ManagementCompanyRequestEventType.StatusChanged, old, next, a.UserId, reason, now);
        db.ManagementCompanyRequestHistories.Add(history);
        await SaveConcurrency(ct);
        return history;
    }

    public async Task<ManagementCompanyRequestHistory> CompletePaymentAsync(ManagementCompanyRequest r, ManagementCompanyRequestActor a, IReadOnlyList<IFormFile>? files, string? reason, CancellationToken ct)
    {
        if (r.Type != ManagementCompanyRequestType.Payment) throw new ValidationAppException("Esta ação só se aplica a solicitações de pagamento.");
        var old = r.Status; var now = DateTime.UtcNow;
        try { r.Complete(a.UserId, now); }
        catch (InvalidOperationException e) { throw new ConflictAppException(e.Message); }
        var history = new ManagementCompanyRequestHistory(r.Id, ManagementCompanyRequestEventType.Completed, old, r.Status, a.UserId, reason, now);
        db.ManagementCompanyRequestHistories.Add(history);
        var saved = new List<string>();
        try
        {
            await SaveFiles(r.Id, null, a.UserId, files, saved, ManagementCompanyRequestAttachmentPurpose.PaymentReceipt, ct);
            await SaveConcurrency(ct);
            return history;
        }
        catch
        {
            foreach (var key in saved) storage.Delete(key);
            throw;
        }
    }

    public async Task<ManagementCompanyRequestHistory> CancelAsync(ManagementCompanyRequest r, ManagementCompanyRequestActor a, string reason, CancellationToken ct)
    {
        var old = r.Status; var now = DateTime.UtcNow;
        try { r.Cancel(a.UserId, reason, now); }
        catch (ArgumentException e) { throw new ValidationAppException(e.Message); }
        catch (InvalidOperationException e) { throw new ConflictAppException(e.Message); }
        var history = new ManagementCompanyRequestHistory(r.Id, ManagementCompanyRequestEventType.Cancelled, old, r.Status, a.UserId, reason, now);
        db.ManagementCompanyRequestHistories.Add(history);
        await SaveConcurrency(ct);
        return history;
    }

    public Task<ManagementCompanyRequestInteractionResult> AddMessageAsync(ManagementCompanyRequest r, ManagementCompanyRequestActor a, string content, CancellationToken ct)
        => InteractAsync(r, a, content, null, null, ct);

    public async Task<ManagementCompanyRequestInteractionResult> InteractAsync(ManagementCompanyRequest r, ManagementCompanyRequestActor a, string content, IReadOnlyList<IFormFile>? files, ManagementCompanyRequestStatus? target, CancellationToken ct)
    {
        if (r.IsTerminal) throw new ConflictAppException("Solicitações concluídas ou canceladas são somente leitura.");
        if (target.HasValue && target != ManagementCompanyRequestStatus.Submitted)
            throw new ValidationAppException("A conversa não altera o status da solicitação.");
        ManagementCompanyRequestMessage m;
        try { m = new(r.Id, a.UserId, content); }
        catch (ArgumentException e) { throw new ValidationAppException(e.Message); }
        Validate(files, await db.ManagementCompanyRequestAttachments.CountAsync(x => x.RequestId == r.Id, ct));
        var saved = new List<string>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.ManagementCompanyRequestMessages.Add(m);
            await SaveFiles(r.Id, m.Id, a.UserId, files, saved, ManagementCompanyRequestAttachmentPurpose.Message, ct);
            await SaveConcurrency(ct);
            await tx.CommitAsync(ct);
            return new(m, null);
        }
        catch
        {
            foreach (var key in saved) storage.Delete(key);
            throw;
        }
    }

    private static void Validate(IReadOnlyList<IFormFile>? files, int existing)
    {
        if (files is null || files.Count == 0) return;
        if (existing + files.Count > AttachmentPolicy.MaximumFileCount)
            throw new ValidationAppException($"É permitido manter no máximo {AttachmentPolicy.MaximumFileCount} anexos por solicitação.");
        foreach (var f in files)
        {
            var v = AttachmentPolicy.Validate(f.FileName, f.Length, f.ContentType);
            if (v.Error is not null) throw new ValidationAppException(v.Error);
        }
    }

    private async Task SaveFiles(Guid rid, Guid? mid, Guid uid, IReadOnlyList<IFormFile>? files, List<string> saved, ManagementCompanyRequestAttachmentPurpose purpose, CancellationToken ct)
    {
        if (files is null) return;
        foreach (var f in files)
        {
            var v = AttachmentPolicy.Validate(f.FileName, f.Length, f.ContentType);
            var key = await storage.SaveManagementCompanyRequestAsync(rid, f, v.Extension!, ct);
            saved.Add(key);
            db.ManagementCompanyRequestAttachments.Add(new(rid, uid, v.Name!, key, v.ContentType!, f.Length, mid, purpose));
        }
    }

    private async Task SaveConcurrency(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictAppException("A solicitação foi alterada simultaneamente. Atualize os dados e tente novamente."); }
    }
}

public sealed record CreateFineCommand(Guid CondominiumId, Guid CategoryId, Guid UnitId, string Nature, string Description, DateOnly OccurrenceDate, decimal? Value, bool ValueNotDefined);
public sealed record CreatePaymentCommand(Guid CondominiumId, Guid CategoryId, string Nature, decimal Value, DateOnly EventDate, DateOnly? DueDate, bool IsReimbursement, Guid? BeneficiaryUserId, string? Notes, string? ThirdPartyIdentification, ManagementCompanyPaymentThirdPartyForm? ThirdPartyForm, string? ThirdPartyPixKey, string? ThirdPartyBank, string? ThirdPartyAgency, string? ThirdPartyAccount);
public sealed record CreateQuestionCommand(Guid CondominiumId, Guid CategoryId, string Theme, string Message);
/// <summary>The message an interaction always creates, and the status-changing history row it created, if any.</summary>
public sealed record ManagementCompanyRequestInteractionResult(ManagementCompanyRequestMessage Message, ManagementCompanyRequestHistory? History);
