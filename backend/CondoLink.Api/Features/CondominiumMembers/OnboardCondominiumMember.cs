using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CondoLink.Api.Features.CondominiumMembers;

public static class OnboardCondominiumMember
{
    public static IEndpointRouteBuilder MapOnboardCondominiumMember(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/condominiums/{condominiumId:guid}/members/onboard", HandleAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(Guid condominiumId, Request request,
        ClaimsPrincipal principal, ResidentOnboardingService onboardingService,
        CancellationToken cancellationToken)
    {
        var claim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var authenticatedUserId))
            return Results.Json(new { error = "Invalid authenticated user." }, statusCode: 401);

        var result = await onboardingService.OnboardAsync(authenticatedUserId, condominiumId,
            new ResidentOnboardingInput(request.FullName, request.Email, request.PhoneNumber, request.UnitId,
                request.RelationshipType, request.IsResident, request.IsPrimaryResidence, request.FirstAccessChannel,
                request.EmailDeliveryEnabled, request.SendAccessEmail, request.InvitationOperationId), cancellationToken);
        if (!result.Succeeded)
            return ToHttpResult(result);

        var user = result.User!;
        var membership = result.Membership!;
        var unitMembership = result.UnitMembership;
        return Results.Created($"/users/{user.Id}", new Response(
            new UserResponse(user.Id, user.FullName, user.Email!, user.PhoneNumber, user.IsActive),
            new MembershipResponse(membership.Id, membership.CondominiumId, membership.IsActive, membership.JoinedAt),
            ["Resident"],
            unitMembership is null ? null : new UnitMembershipResponse(unitMembership.Id, unitMembership.UnitId,
                unitMembership.RelationshipType.ToString(), unitMembership.IsResident, unitMembership.IsPrimaryResidence),
            result.IsNewUser,
            result.WhatsAppQueued ? "InviteQueued" : result.EmailSent ? "InviteSent"
                : user.MustChangePassword ? "Pending" : "Completed",
            result.EmailSent,
            result.WhatsAppQueued));
    }

    private static IResult ToHttpResult(ResidentOnboardingResult result)
    {
        if (result.Error == ResidentOnboardingError.Validation)
            return Results.BadRequest(new { errors = result.ValidationErrors });

        return result.Error switch
        {
            ResidentOnboardingError.AuthenticatedUserNotFound => Results.Json(new { error = result.ErrorMessage }, statusCode: 401),
            ResidentOnboardingError.Forbidden => Results.Json(new { error = result.ErrorMessage }, statusCode: 403),
            ResidentOnboardingError.CondominiumNotFound or ResidentOnboardingError.UnitNotFound => Results.NotFound(new { error = result.ErrorMessage }),
            ResidentOnboardingError.InactiveCondominium or ResidentOnboardingError.InactiveUser
                or ResidentOnboardingError.DuplicatePhoneNumber or ResidentOnboardingError.InactiveUnit
                or ResidentOnboardingError.DuplicateEmail or ResidentOnboardingError.InactiveMembership
                or ResidentOnboardingError.InactiveResidentRole => Results.Conflict(new { error = result.ErrorMessage }),
            _ => Results.BadRequest(new { error = result.ErrorMessage })
        };
    }

    public sealed record Request(string? FullName, string? Email, string? PhoneNumber, Guid? UnitId,
        string? RelationshipType, bool IsResident, bool IsPrimaryResidence,
        string? FirstAccessChannel = null, bool EmailDeliveryEnabled = false,
        bool SendAccessEmail = false, string? InvitationOperationId = null);
    public sealed record UserResponse(Guid Id, string FullName, string Email, string? PhoneNumber, bool IsActive);
    public sealed record MembershipResponse(Guid Id, Guid CondominiumId, bool IsActive, DateTime JoinedAt);
    public sealed record UnitMembershipResponse(Guid Id, Guid UnitId, string RelationshipType, bool IsResident, bool IsPrimaryResidence);
    public sealed record Response(UserResponse User, MembershipResponse Membership, IReadOnlyList<string> Roles,
        UnitMembershipResponse? UnitMembership, bool IsNewUser, string FirstAccessStatus,
        bool EmailSent = false, bool WhatsAppQueued = false);
}
