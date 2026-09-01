using CondoLink.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class SignalRAuthenticationConfigurationTests
{
    [Fact]
    public async Task Jwt_bearer_reads_access_token_only_for_management_request_hub()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Jwt:Issuer"] = "tests",
            ["Jwt:Audience"] = "tests",
            ["Jwt:Key"] = "test-signing-key-with-at-least-32-bytes",
            ["Jwt:ExpirationMinutes"] = "60"
        }).Build();
        services.AddInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

        var hubContext = new DefaultHttpContext();
        hubContext.Request.Path = "/management-company-requests/realtime/negotiate";
        hubContext.Request.QueryString = new QueryString("?access_token=hub-token");
        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var hubMessage = new MessageReceivedContext(hubContext, scheme, options);
        await options.Events!.OnMessageReceived!(hubMessage);
        Assert.Equal("hub-token", hubMessage.Token);

        var apiContext = new DefaultHttpContext();
        apiContext.Request.Path = "/management-company-requests/123";
        apiContext.Request.QueryString = new QueryString("?access_token=must-not-be-used");
        var apiMessage = new MessageReceivedContext(apiContext, scheme, options);
        await options.Events.OnMessageReceived(apiMessage);
        Assert.Null(apiMessage.Token);
    }
}
