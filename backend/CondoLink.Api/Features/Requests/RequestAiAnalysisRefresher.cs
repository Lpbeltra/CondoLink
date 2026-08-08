using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.Notifications;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public sealed class RequestAiAnalysisRefresher(
    AppDbContext db,
    IRequestDraftAiService ai,
    ILogger<RequestAiAnalysisRefresher> logger)
{
    public async Task RefreshAsync(Guid requestId, string trigger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Assistant refresh started. RequestId: {RequestId}; Trigger: {Trigger}.",
            requestId, trigger);
        try
        {
            var request = await db.Requests.AsNoTracking()
                .Where(item => item.Id == requestId)
                .Select(item => new
                {
                    item.Title, item.Description, item.Status, item.Priority,
                    item.CategoryId, item.CondominiumId
                }).SingleOrDefaultAsync(cancellationToken);
            if (request is null) return;
            var condominiumName = await db.Condominiums.AsNoTracking()
                .Where(item => item.Id == request.CondominiumId)
                .Select(item => item.Name).SingleAsync(cancellationToken);
            var categories = await db.Categories.AsNoTracking()
                .Where(item => item.CondominiumId == request.CondominiumId && item.IsActive)
                .OrderBy(item => item.Name).Select(item => item.Name)
                .ToArrayAsync(cancellationToken);
            var category = await db.Categories.AsNoTracking()
                .Where(item => item.Id == request.CategoryId)
                .Select(item => item.Name).SingleAsync(cancellationToken);
            var messages = await db.RequestMessages.AsNoTracking()
                .Where(item => item.RequestId == requestId)
                .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
                .Select(item => item.Content).ToArrayAsync(cancellationToken);
            var attachments = await db.RequestAttachments.AsNoTracking()
                .Where(item => item.RequestId == requestId)
                .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
                .Select(item => new { item.ContentType, item.OriginalFileName })
                .ToArrayAsync(cancellationToken);

            var context = new StringBuilder()
                .AppendLine($"Título atual: {request.Title}")
                .AppendLine($"Descrição atual: {request.Description}")
                .AppendLine($"Status atual: {NotificationService.Describe(request.Status)}")
                .AppendLine($"Prioridade atual: {request.Priority}")
                .AppendLine($"Categoria atual: {category}");
            if (messages.Length > 0)
            {
                context.AppendLine("Mensagens em ordem cronológica:");
                foreach (var message in messages)
                    context.AppendLine($"- {message}");
            }
            if (attachments.Length > 0)
            {
                context.AppendLine("Anexos:");
                foreach (var attachment in attachments)
                    context.AppendLine($"- {attachment.ContentType}: {attachment.OriginalFileName}");
            }

            var result = await ai.ProposeAsync(context.ToString(), categories,
                condominiumName, cancellationToken);
            if (!result.Succeeded || result.Proposal is null)
            {
                logger.LogWarning(
                    "Assistant refresh failed. RequestId: {RequestId}; Trigger: {Trigger}; Outcome: {Outcome}.",
                    requestId, trigger, result.Outcome);
                return;
            }
            var proposal = result.Proposal;
            var missing = JsonSerializer.Serialize(proposal.MissingInformation);
            var analysis = await db.RequestAiAnalyses
                .SingleOrDefaultAsync(item => item.RequestId == requestId,
                    cancellationToken);
            if (analysis is null)
                db.RequestAiAnalyses.Add(new RequestAiAnalysis(requestId,
                    proposal.Title, proposal.Description, proposal.SuggestedCategory,
                    proposal.Confidence, missing, result.Model));
            else
                analysis.Refresh(proposal.Title, proposal.Description,
                    proposal.SuggestedCategory, proposal.Confidence, missing, result.Model);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Assistant refresh succeeded. RequestId: {RequestId}; Trigger: {Trigger}.",
                requestId, trigger);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Assistant refresh failed safely. RequestId: {RequestId}; Trigger: {Trigger}; FailureType: {FailureType}.",
                requestId, trigger, exception.GetType().Name);
        }
    }
}
