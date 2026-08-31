namespace CondoLink.Api.Features.Auth;
public static class RefreshEndpoints
{
 public static IEndpointRouteBuilder MapRefreshEndpoints(this IEndpointRouteBuilder endpoints){endpoints.MapPost("/auth/refresh",RefreshAsync).WithTags("Authentication");endpoints.MapPost("/auth/logout",LogoutAsync).WithTags("Authentication");return endpoints;}
 private static async Task<IResult> RefreshAsync(HttpRequest request,HttpResponse response,AuthenticationSessionService sessions,CancellationToken ct){var result=await sessions.RefreshAsync(request.Cookies[AuthenticationSessionService.CookieName],response,ct);return result is null?Results.Json(new{error="Session is invalid or expired."},statusCode:401):Results.Ok(result);}
 private static async Task<IResult> LogoutAsync(HttpRequest request,HttpResponse response,AuthenticationSessionService sessions,CancellationToken ct){await sessions.RevokeAsync(request.Cookies[AuthenticationSessionService.CookieName],response,ct);return Results.NoContent();}
}
