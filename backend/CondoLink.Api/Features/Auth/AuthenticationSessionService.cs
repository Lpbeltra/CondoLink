using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CondoLink.Api.Features.Auth;

public sealed class AuthenticationSessionOptions { public const string SectionName="AuthenticationSession"; public int RefreshTokenDays { get; set; }=30; }

public sealed class AuthenticationSessionService(UserManager<ApplicationUser> users,AppDbContext db,IConfiguration configuration,IOptions<AuthenticationSessionOptions> options,TimeProvider clock,ILogger<AuthenticationSessionService> logger)
{
 public const string CookieName="comvy.refresh";
 public async Task<Login.Response?> IssueAsync(ApplicationUser user,HttpResponse response,CancellationToken ct){var now=clock.GetUtcNow().UtcDateTime;user.MarkSuccessfulLogin(now);if(!(await users.UpdateAsync(user)).Succeeded)return null;var result=await CreateAccessTokenAsync(user,now);var raw=NewToken();var expiry=now.AddDays(options.Value.RefreshTokenDays);db.RefreshSessions.Add(new(user.Id,Hash(raw),user.SecurityStamp??string.Empty,now,expiry));await db.SaveChangesAsync(ct);WriteCookie(response,raw,expiry);return result;}
 public async Task<Login.Response?> RefreshAsync(string? raw,HttpResponse response,CancellationToken ct){if(string.IsNullOrWhiteSpace(raw))return null;var now=clock.GetUtcNow().UtcDateTime;var hash=Hash(raw);await using var tx=await db.Database.BeginTransactionAsync(ct);var current=await db.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(x=>x.TokenHash==hash,ct);if(current is null){logger.LogInformation("Refresh failed: unknown token.");return null;}if(current.RevokedAt is not null){logger.LogWarning("Refresh reuse detected for SessionId {SessionId}.",current.Id);return null;}if(current.ExpiresAt<=now){logger.LogInformation("Refresh failed: expired SessionId {SessionId}.",current.Id);return null;}var user=await users.FindByIdAsync(current.UserId.ToString());if(user is null||!user.IsActive||user.MustChangePassword||current.SecurityStamp!=(user.SecurityStamp??string.Empty)){logger.LogInformation("Refresh failed: invalid user state for SessionId {SessionId}.",current.Id);return null;}var newRaw=NewToken();var replacement=new RefreshSession(user.Id,Hash(newRaw),user.SecurityStamp??string.Empty,now,now.AddDays(options.Value.RefreshTokenDays));db.RefreshSessions.Add(replacement);var changed=await db.RefreshSessions.Where(x=>x.Id==current.Id&&x.RevokedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RevokedAt,now).SetProperty(x=>x.LastUsedAt,now).SetProperty(x=>x.ReplacedBySessionId,replacement.Id),ct);if(changed!=1){await tx.RollbackAsync(ct);logger.LogWarning("Refresh reuse detected concurrently for SessionId {SessionId}.",current.Id);return null;}await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);WriteCookie(response,newRaw,replacement.ExpiresAt);logger.LogInformation("Refresh succeeded for UserId {UserId}, SessionId {SessionId}.",user.Id,replacement.Id);return await CreateAccessTokenAsync(user,now);}
 public async Task RevokeAsync(string? raw,HttpResponse response,CancellationToken ct){if(!string.IsNullOrWhiteSpace(raw)){var now=clock.GetUtcNow().UtcDateTime;var hash=Hash(raw);var changed=await db.RefreshSessions.Where(x=>x.TokenHash==hash&&x.RevokedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RevokedAt,now),ct);if(changed>0)logger.LogInformation("Refresh session revoked.");}response.Cookies.Delete(CookieName,CookieOptions());}
 private async Task<Login.Response> CreateAccessTokenAsync(ApplicationUser user,DateTime now){var minutes=configuration.GetValue<int>("Jwt:ExpirationMinutes");var roles=await users.GetRolesAsync(user);var claims=new List<Claim>{new(JwtRegisteredClaimNames.Sub,user.Id.ToString()),new(ClaimTypes.NameIdentifier,user.Id.ToString()),new(JwtRegisteredClaimNames.Email,user.Email!),new(ClaimTypes.Name,user.FullName),new(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString()),new(JwtRegisteredClaimNames.Iat,EpochTime.GetIntDate(now).ToString(),ClaimValueTypes.Integer64)};claims.AddRange(roles.Select(x=>new Claim(ClaimTypes.Role,x)));var token=new JwtSecurityToken(configuration["Jwt:Issuer"],configuration["Jwt:Audience"],claims,now,now.AddMinutes(minutes),new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)),SecurityAlgorithms.HmacSha256));return new(false,new JwtSecurityTokenHandler().WriteToken(token),"Bearer",checked(minutes*60),new(user.Id,user.FullName,user.Email!,user.IsActive,roles.ToList()));}
 private static string NewToken()=>Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
 private static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
 private static CookieOptions CookieOptions(bool secure=false)=>new(){HttpOnly=true,Secure=secure,SameSite=SameSiteMode.Lax,Path="/auth",IsEssential=true};
 private static void WriteCookie(HttpResponse response,string token,DateTime expires){var o=CookieOptions(response.HttpContext.Request.IsHttps);o.Expires=new DateTimeOffset(expires,TimeSpan.Zero);response.Cookies.Append(CookieName,token,o);}
}
