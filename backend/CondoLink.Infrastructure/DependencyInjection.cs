using System.Text;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace CondoLink.Infrastructure;

public static class DependencyInjection
{
    public const string PlatformAdminPolicy = "PlatformAdmin";
    public const string PlatformAdminRole = "PlatformAdmin";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        var jwtIssuer = configuration["Jwt:Issuer"];
        var jwtAudience = configuration["Jwt:Audience"];
        var jwtKey = configuration["Jwt:Key"];
        var jwtExpirationMinutes =
            configuration.GetValue<int>("Jwt:ExpirationMinutes");

        if (string.IsNullOrWhiteSpace(jwtIssuer))
        {
            throw new InvalidOperationException(
                "JWT issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(jwtAudience))
        {
            throw new InvalidOperationException(
                "JWT audience is not configured.");
        }

        if (string.IsNullOrWhiteSpace(jwtKey)
            || Encoding.UTF8.GetByteCount(jwtKey) < 32)
        {
            throw new InvalidOperationException(
                "JWT key must contain at least 32 bytes.");
        }

        if (jwtExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT expiration minutes must be greater than zero.");
        }

        services.AddDbContext<AppDbContext>(
            options => options.UseNpgsql(connectionString));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtIssuer,

                        ValidateAudience = true,
                        ValidAudience = jwtAudience,

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey =
                            new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(jwtKey)),

                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,

                        NameClaimType = ClaimTypes.Name,
                        RoleClaimType = ClaimTypes.Role
                    };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                PlatformAdminPolicy,
                policy => policy.RequireRole(PlatformAdminRole));
        });

        return services;
    }
}