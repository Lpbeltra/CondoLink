using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CondoLink.Api.Features.Requests;

public static class CreateResidentReply
{
    public static IEndpointRouteBuilder MapCreateResidentReply(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/requests/{requestId:guid}/resident-reply", HandleAsync)
            .RequireAuthorization().DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(AttachmentPolicy.MaximumRequestSize));
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(Guid requestId, HttpRequest request,
        ClaimsPrincipal principal, ResidentReplyService service, CancellationToken cancellationToken)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId)) return Results.Unauthorized();
        if (!request.HasFormContentType)
            return Results.BadRequest(new { error = "Envie a resposta usando multipart/form-data." });
        IFormCollection form;
        try { form = await request.ReadFormAsync(cancellationToken); }
        catch (InvalidDataException) { return Results.BadRequest(new { error = "Não foi possível ler a resposta enviada." }); }
        var files = form.Files.GetFiles("files").Select(file =>
            new ResidentReplyService.ReplyFile(file.FileName, file.ContentType, file.Length,
                _ => Task.FromResult<Stream>(file.OpenReadStream()))).ToArray();
        var result = await service.ReplyAsync(requestId, userId, form["message"].FirstOrDefault(),
            files, MessageChannel.Portal, cancellationToken);
        return result.Code switch
        {
            ResidentReplyService.ResultCode.Succeeded => Results.Ok(new { messageId = result.MessageId, status = "InProgress" }),
            ResidentReplyService.ResultCode.Invalid => Results.BadRequest(new { error = result.Error }),
            ResidentReplyService.ResultCode.NotFound => Results.NotFound(new { error = result.Error }),
            ResidentReplyService.ResultCode.Forbidden => Results.Json(new { error = result.Error }, statusCode: 403),
            _ => Results.Conflict(new { error = result.Error })
        };
    }
}
