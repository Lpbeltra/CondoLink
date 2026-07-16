using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.RequestAttachments;

public static class RequestAttachmentEndpoints
{
    private const long MaximumFileSize = 10 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string[]> AllowedFiles =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"],
            [".png"] = ["image/png"],
            [".webp"] = ["image/webp"],
            [".pdf"] = ["application/pdf"]
        };

    public static IEndpointRouteBuilder MapRequestAttachments(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/requests/{requestId:guid}/attachments", UploadAsync)
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(52 * 1024 * 1024));
        endpoints.MapGet("/requests/{requestId:guid}/attachments", ListAsync).RequireAuthorization();
        endpoints.MapGet("/request-attachments/{attachmentId:guid}/content", ContentAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> UploadAsync(Guid requestId, HttpRequest request,
        ClaimsPrincipal principal, AppDbContext dbContext, LocalFileStorage storage,
        CancellationToken cancellationToken)
    {
        var access = await CheckAccessAsync(requestId, principal, dbContext, cancellationToken);
        if (access.Error is not null) return access.Error;
        if (access.Status == RequestStatus.Cancelled)
            return Results.Conflict(new { error = "Cancelled requests cannot receive attachments." });
        if (!request.HasFormContentType)
            return Results.BadRequest(new { error = "At least one file is required." });

        IFormCollection form;
        try { form = await request.ReadFormAsync(cancellationToken); }
        catch (InvalidDataException)
        {
            return Results.BadRequest(new { error = "At least one file is required." });
        }
        var files = form.Files.GetFiles("files");
        if (files.Count == 0) return Results.BadRequest(new { error = "At least one file is required." });
        if (files.Count > 5) return Results.BadRequest(new { error = "A maximum of five files is allowed." });

        var validated = new List<(IFormFile File, string Name, string Extension)>();
        foreach (var file in files)
        {
            var name = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(name);
            if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
                return Results.BadRequest(new { error = "File name is invalid or exceeds 255 characters." });
            if (file.Length <= 0) return Results.BadRequest(new { error = $"File '{name}' is empty." });
            if (file.Length > MaximumFileSize)
                return Results.BadRequest(new { error = $"File '{name}' exceeds the 10 MB limit." });
            if (!AllowedFiles.TryGetValue(extension, out var contentTypes)
                || !contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = $"File '{name}' has an invalid type or extension." });
            validated.Add((file, name, extension.ToLowerInvariant()));
        }

        var savedKeys = new List<string>();
        try
        {
            var attachments = new List<RequestAttachment>();
            foreach (var item in validated)
            {
                var key = await storage.SaveAsync(requestId, item.File, item.Extension, cancellationToken);
                savedKeys.Add(key);
                attachments.Add(new RequestAttachment(requestId, access.UserId, item.Name, key,
                    item.File.ContentType.ToLowerInvariant(), item.File.Length));
            }
            dbContext.RequestAttachments.AddRange(attachments);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Created($"/requests/{requestId}/attachments",
                attachments.Select(x => ToResponse(x, access.FullName)).ToArray());
        }
        catch
        {
            foreach (var key in savedKeys) storage.Delete(key);
            throw;
        }
    }

    private static async Task<IResult> ListAsync(Guid requestId, ClaimsPrincipal principal,
        AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var access = await CheckAccessAsync(requestId, principal, dbContext, cancellationToken);
        if (access.Error is not null) return access.Error;

        var rows = await dbContext.RequestAttachments.AsNoTracking()
            .Where(x => x.RequestId == requestId)
            .Join(dbContext.Set<ApplicationUser>().AsNoTracking(), x => x.UploadedByUserId, u => u.Id,
                (x, u) => new { Attachment = x, u.FullName })
            .OrderBy(x => x.Attachment.CreatedAt).ThenBy(x => x.Attachment.Id)
            .ToListAsync(cancellationToken);
        return Results.Ok(rows.Select(x => ToResponse(x.Attachment, x.FullName)).ToArray());
    }

    private static async Task<IResult> ContentAsync(Guid attachmentId, ClaimsPrincipal principal,
        HttpResponse response, AppDbContext dbContext, LocalFileStorage storage,
        CancellationToken cancellationToken)
    {
        var attachment = await dbContext.RequestAttachments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == attachmentId, cancellationToken);
        if (attachment is null) return Results.NotFound(new { error = "Attachment not found." });
        var access = await CheckAccessAsync(attachment.RequestId, principal, dbContext, cancellationToken);
        if (access.Error is not null) return access.Error;

        FileStream? stream;
        try { stream = storage.OpenRead(attachment.StorageKey); }
        catch (InvalidOperationException) { stream = null; }
        if (stream is null) return Results.NotFound(new { error = "Attachment file was not found." });
        response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(attachment.OriginalFileName)}";
        response.Headers.CacheControl = "no-store";
        return Results.File(stream, attachment.ContentType, enableRangeProcessing: true);
    }

    private static Response ToResponse(RequestAttachment x, string fullName) => new(x.Id, x.RequestId,
        x.OriginalFileName, x.ContentType, x.FileSize, new UploadedByResponse(x.UploadedByUserId, fullName),
        x.CreatedAt, $"/request-attachments/{x.Id}/content");

    private static async Task<AccessCheck> CheckAccessAsync(Guid requestId, ClaimsPrincipal principal,
        AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
            return new(Guid.Empty, "", default, Results.Json(new { error = "Invalid authenticated user." }, statusCode: 401));
        var user = await dbContext.Set<ApplicationUser>().AsNoTracking().Where(x => x.Id == userId)
            .Select(x => new { x.IsActive, x.FullName }).SingleOrDefaultAsync(cancellationToken);
        if (user is null)
            return new(userId, "", default, Results.Json(new { error = "Authenticated user was not found." }, statusCode: 401));
        if (!user.IsActive)
            return new(userId, user.FullName, default, Results.Json(new { error = "User account is inactive." }, statusCode: 403));
        var target = await dbContext.Requests.AsNoTracking().Where(x => x.Id == requestId)
            .Select(x => new { x.AuthorUserId, x.CondominiumId, x.Status }).SingleOrDefaultAsync(cancellationToken);
        if (target is null)
            return new(userId, user.FullName, default, Results.NotFound(new { error = "Request not found." }));
        if (target.AuthorUserId != userId)
        {
            var manager = await dbContext.CondominiumMemberships.AsNoTracking()
                .Where(x => x.UserId == userId && x.CondominiumId == target.CondominiumId && x.IsActive && x.EndedAt == null)
                .Join(dbContext.CondominiumMembershipRoles.AsNoTracking().Where(x => x.Role == CondominiumRole.Manager && x.IsActive && x.RevokedAt == null),
                    x => x.Id, x => x.CondominiumMembershipId, (_, _) => true).AnyAsync(cancellationToken);
            if (!manager)
                return new(userId, user.FullName, target.Status, Results.Json(new { error = "You do not have access to this request." }, statusCode: 403));
        }
        return new(userId, user.FullName, target.Status, null);
    }

    private sealed record AccessCheck(Guid UserId, string FullName, RequestStatus Status, IResult? Error);
    public sealed record UploadedByResponse(Guid Id, string FullName);
    public sealed record Response(Guid Id, Guid RequestId, string OriginalFileName, string ContentType,
        long FileSize, UploadedByResponse UploadedBy, DateTime CreatedAt, string ContentUrl);
}
