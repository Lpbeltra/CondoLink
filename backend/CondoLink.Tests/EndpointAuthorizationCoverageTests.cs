using System.Reflection;
using CondoLink.Api;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Blocks;
using CondoLink.Api.Features.Categories;
using CondoLink.Api.Features.CondominiumMemberRoles;
using CondoLink.Api.Features.CondominiumMembers;
using CondoLink.Api.Features.Condominiums;
using CondoLink.Api.Features.CondominiumSetup;
using CondoLink.Api.Features.Management;
using CondoLink.Api.Features.Notifications;
using CondoLink.Api.Features.Overwatch;
using CondoLink.Api.Features.Reports;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.RequestMessages;
using CondoLink.Api.Features.Requests;
using CondoLink.Api.Features.UnitMemberships;
using CondoLink.Api.Features.Units;
using CondoLink.Api.Features.Overwatch.Managers;
using CondoLink.Api.Features.Users;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CondoLink.Tests;

/// <summary>
/// Guards the pre-authentication attack surface as a whole.
///
/// Endpoints opt into authorization individually and there is no global
/// fallback policy, so forgetting <c>.RequireAuthorization()</c> on a single
/// route silently publishes it. This test enumerates every registered endpoint
/// and fails when an unexpected one is anonymous, so the class of bug cannot
/// come back unnoticed.
/// </summary>
public sealed class EndpointAuthorizationCoverageTests
{
    /// <summary>
    /// The only routes allowed to be reachable without a token.
    /// Adding to this list is a deliberate, reviewable decision.
    /// </summary>
    private static readonly HashSet<string> IntentionallyPublicRoutes = new(
        StringComparer.OrdinalIgnoreCase)
    {
        // Callers have no token yet by definition.
        "/auth/login",
        // The temporary credential itself authenticates this one-time flow.
        "/auth/change-temporary-password",
        // Liveness probe: reports only reachability, no tenant data.
        "/health",
    };

    [Fact]
    public void Every_endpoint_except_the_public_allow_list_requires_authorization()
    {
        var anonymous = RegisteredEndpoints()
            .Where(endpoint => !endpoint.RequiresAuthorization)
            .Where(endpoint => !IntentionallyPublicRoutes.Contains(endpoint.Route))
            .Select(endpoint => $"{endpoint.Methods} {endpoint.Route}")
            .OrderBy(description => description, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            anonymous.Length == 0,
            "These endpoints are reachable without authentication. Add "
            + ".RequireAuthorization() (or extend IntentionallyPublicRoutes if "
            + "the route is genuinely public):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, anonymous));
    }

    [Fact]
    public void Tenant_listing_endpoints_are_not_public()
    {
        // These leaked the full condominium roster (name, e-mail, phone) plus the
        // ids used by every other route. Pinned explicitly because the payload is
        // the platform's customer list.
        AssertProtected("/condominiums");
        AssertProtected("/condominiums/{id:guid}");
    }

    [Fact]
    public void Account_creation_is_not_public()
    {
        // There is no self-registration flow: residents are onboarded by a
        // manager and managers by a platform admin. Left open, this allowed
        // unlimited anonymous account creation.
        AssertProtected("/users");
    }

    [Fact]
    public void Every_overwatch_endpoint_requires_the_platform_admin_policy()
    {
        var unguarded = RegisteredEndpoints()
            .Where(endpoint => endpoint.Route.StartsWith(
                "/overwatch", StringComparison.OrdinalIgnoreCase))
            .Where(endpoint => !endpoint.PolicyNames.Contains("PlatformAdmin"))
            .Select(endpoint => $"{endpoint.Methods} {endpoint.Route}")
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            "Overwatch endpoints must require the PlatformAdmin policy:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, unguarded));
    }

    [Fact]
    public void The_public_allow_list_still_matches_reality()
    {
        // Catches a stale allow-list entry, e.g. a route that was renamed or
        // removed while its exemption stayed behind.
        var routes = RegisteredEndpoints()
            .Select(endpoint => endpoint.Route)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stale = IntentionallyPublicRoutes
            .Where(route => !routes.Contains(route))
            .ToArray();

        Assert.True(
            stale.Length == 0,
            "These allow-listed routes no longer exist: "
            + string.Join(", ", stale));
    }

    private static void AssertProtected(string route)
    {
        var endpoint = RegisteredEndpoints()
            .FirstOrDefault(item => string.Equals(
                item.Route, route, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(endpoint);
        Assert.True(
            endpoint!.RequiresAuthorization,
            $"{route} must not be reachable without authentication.");
    }

    private sealed record EndpointInfo(
        string Route,
        string Methods,
        bool RequiresAuthorization,
        IReadOnlyCollection<string> PolicyNames);

    /// <summary>
    /// Builds the real endpoint graph the way Program.cs does and reads the
    /// authorization metadata off it, so this tracks production wiring rather
    /// than a copy of it.
    /// </summary>
    private static IReadOnlyList<EndpointInfo> RegisteredEndpoints()
    {
        // Materialising endpoints runs parameter inference, so every handler
        // dependency must be resolvable — even though no request is executed.
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddDbContext<AppDbContext>(
            options => options.UseSqlite("Data Source=:memory:"));
        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        builder.Services.AddScoped<CondominiumMembershipService>();
        builder.Services.AddScoped<ManagerOnboardingService>();
        builder.Services.AddScoped<NotificationService>();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<LocalFileStorage>();

        var app = builder.Build();

        MapAllFeatureEndpoints(app);

        // Read the builder's own data sources: the DI-registered
        // EndpointDataSource is not the instance these Map* calls populate.
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .ToArray();

        return endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => new EndpointInfo(
                "/" + endpoint.RoutePattern.RawText!.TrimStart('/'),
                string.Join(
                    ",",
                    endpoint.Metadata
                        .GetMetadata<HttpMethodMetadata>()?.HttpMethods
                        ?? ["ANY"]),
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0,
                endpoint.Metadata
                    .GetOrderedMetadata<IAuthorizeData>()
                    .Select(data => data.Policy)
                    .Where(policy => !string.IsNullOrWhiteSpace(policy))
                    .Select(policy => policy!)
                    .ToArray()))
            .ToArray();
    }

    /// <summary>
    /// Mirrors the registration list in Program.cs. If a new feature is mapped
    /// there but not here, <see cref="Program_maps_no_endpoints_this_test_misses"/>
    /// fails.
    /// </summary>
    private static void MapAllFeatureEndpoints(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok());

        app.MapLogin();
        app.MapChangeTemporaryPassword();

        app.MapCreateUser();
        app.MapGetCurrentUser();
        app.MapListMyCondominiums();

        app.MapCreateCondominium();
        app.MapGetCondominiumById();
        app.MapListCondominiums();

        app.MapAddCondominiumMember();
        app.MapAddCondominiumMemberRole();
        app.MapListCondominiumMembers();
        app.MapOnboardCondominiumMember();
        app.MapResetMemberTemporaryPassword();
        app.MapCondominiumSetup();

        app.MapManagementContext();
        app.MapCondominiumBlocks();

        app.MapCreateUnit();
        app.MapGetUnitById();
        app.MapListCondominiumUnits();
        app.MapManageUnit();

        app.MapCreateUnitMembership();
        app.MapListUnitMemberships();
        app.MapManageUnitMembership();

        app.MapCreateCategory();
        app.MapListCondominiumCategories();
        app.MapManageCategory();

        app.MapCreateRequest();
        app.MapGetRequestById();
        app.MapListMyRequests();
        app.MapListCondominiumRequests();
        app.MapUpdateRequestStatus();
        app.MapUpdateRequestPriority();

        app.MapGetRequestReport();
        app.MapNotifications();

        app.MapCreateRequestMessage();
        app.MapListRequestMessages();
        app.MapRequestAttachments();

        app.MapOverwatchEndpoints();
    }

    [Fact]
    public void Program_maps_no_endpoints_this_test_misses()
    {
        // Keeps the mirror above honest: every Map* extension invoked by
        // Program.cs must also be invoked here, otherwise a new endpoint could
        // be added to production and escape the coverage check above.
        var programSource = FindProgramSource();
        var mirrorSource = FindOwnSource();

        var programCalls = MapCallNames(programSource);
        var mirrorCalls = MapCallNames(mirrorSource);

        var missing = programCalls.Except(mirrorCalls).OrderBy(name => name).ToArray();

        Assert.True(
            missing.Length == 0,
            "Program.cs maps endpoints that EndpointAuthorizationCoverageTests "
            + "does not, so they are not covered by the anonymous-access check. "
            + "Add them to MapAllFeatureEndpoints: "
            + string.Join(", ", missing));
    }

    private static HashSet<string> MapCallNames(string source) =>
        System.Text.RegularExpressions.Regex
            .Matches(source, @"\bapp\.(Map[A-Za-z0-9_]+)\s*\(")
            .Select(match => match.Groups[1].Value)
            // Generic route helpers, not feature registrations.
            .Where(name => name is not ("MapGet" or "MapPost" or "MapPut"
                or "MapPatch" or "MapDelete" or "MapOpenApi"))
            .ToHashSet(StringComparer.Ordinal);

    private static string FindProgramSource() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "CondoLink.Api", "Program.cs"));

    private static string FindOwnSource() =>
        File.ReadAllText(Path.Combine(
            RepositoryRoot(), "CondoLink.Tests",
            "EndpointAuthorizationCoverageTests.cs"));

    /// <summary>Walks up from the test assembly to the solution folder.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "CondoLink.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
