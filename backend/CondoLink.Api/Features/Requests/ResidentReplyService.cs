using CondoLink.Api.Features.Notifications;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public sealed class ResidentReplyService(AppDbContext dbContext, LocalFileStorage storage,
    NotificationService notifications,
    RequestAiAnalysisRefresher? analysisRefresher = null)
{
    public async Task<Result> ReplyAsync(Guid requestId, Guid userId, string? text,
        IReadOnlyList<ReplyFile> files, MessageChannel channel, CancellationToken cancellationToken)
    {
        var content = text?.Trim();
        content = string.IsNullOrEmpty(content) ? null : content;
        if (content?.Length > RequestMessage.MaximumContentLength) return new(ResultCode.Invalid, "A resposta deve possuir no máximo 3000 caracteres.");
        if (content is null && files.Count == 0) return new(ResultCode.Invalid, "Informe uma resposta ou selecione ao menos um arquivo.");
        if (files.Count > AttachmentPolicy.MaximumFileCount) return new(ResultCode.Invalid, "É permitido enviar no máximo 10 arquivos.");

        var validated = new List<(ReplyFile File, AttachmentPolicy.ValidationResult Validation)>();
        foreach (var file in files)
        {
            var validation = AttachmentPolicy.Validate(file.FileName, file.Length, file.ContentType);
            if (validation.Error is not null) return new(ResultCode.Invalid, validation.Error);
            validated.Add((file, validation));
        }

        var request = await dbContext.Requests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        var activeUser = await dbContext.Set<ApplicationUser>().AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (!activeUser) return new(ResultCode.Forbidden, "Usuário sem permissão para responder.");
        if (request is null) return new(ResultCode.NotFound, "Solicitação não encontrada.");
        if (request.AuthorUserId != userId) return new(ResultCode.Forbidden, "Você não pode responder esta solicitação.");
        if (request.Status != RequestStatus.WaitingForResident)
            return new(ResultCode.Conflict, "A solicitação não está aguardando uma resposta do morador.");

        var requirementExists = await dbContext.RequestResidentReplyRequirements.AsNoTracking()
            .AnyAsync(x => x.RequestId == requestId && x.IsActive, cancellationToken);
        if (!requirementExists) return new(ResultCode.Conflict, "Não existe uma pendência ativa para esta solicitação.");

        var message = new RequestMessage(requestId, userId,
            content ?? "Anexo enviado pelo morador.", channel);
        var savedKeys = new List<string>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.RequestMessages.Add(message);
            await dbContext.SaveChangesAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var requirementUpdated = await dbContext.RequestResidentReplyRequirements
                .Where(x => x.RequestId == requestId && x.IsActive && x.AnswerMessageId == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.AnsweredAt, now)
                    .SetProperty(x => x.AnswerMessageId, message.Id)
                    .SetProperty(x => x.HasUnreadAnswer, true)
                    .SetProperty(x => x.UpdatedAt, now), cancellationToken);
            var requestUpdated = await dbContext.Requests
                .Where(x => x.Id == requestId && x.Status == RequestStatus.WaitingForResident)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, RequestStatus.InProgress)
                    .SetProperty(x => x.UpdatedAt, now), cancellationToken);
            if (requirementUpdated != 1 || requestUpdated != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(ResultCode.Conflict, "A pendência já foi respondida ou alterada.");
            }

            foreach (var item in validated)
            {
                await using var stream = await item.File.OpenAsync(cancellationToken);
                var key = await storage.SaveAsync(requestId, stream, item.Validation.Extension!, cancellationToken);
                savedKeys.Add(key);
                dbContext.RequestAttachments.Add(new RequestAttachment(requestId, userId,
                    item.Validation.Name!, key, item.Validation.ContentType!, item.File.Length, message.Id));
            }
            dbContext.RequestStatusHistories.Add(new RequestStatusHistory(requestId,
                RequestStatus.WaitingForResident, RequestStatus.InProgress, userId,
                "Resposta recebida do morador.", now));
            await notifications.NotifyMessageAsync(requestId, request.CondominiumId,
                request.AuthorUserId, request.Title, userId, content ?? "Anexo enviado pelo morador.",
                cancellationToken, message.Id, channel);
            await transaction.CommitAsync(cancellationToken);
            if (analysisRefresher is not null)
                await analysisRefresher.RefreshAsync(requestId,
                    "resident_reply", cancellationToken);
            return new(ResultCode.Succeeded, null, message.Id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            foreach (var key in savedKeys) storage.Delete(key);
            throw;
        }
    }

    public sealed record ReplyFile(string FileName, string ContentType, long Length,
        Func<CancellationToken, Task<Stream>> OpenAsync);
    public sealed record Result(ResultCode Code, string? Error, Guid? MessageId = null);
    public enum ResultCode { Succeeded, Invalid, NotFound, Forbidden, Conflict }
}
