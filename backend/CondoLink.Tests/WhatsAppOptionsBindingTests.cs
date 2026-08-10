using CondoLink.Api.Features.WhatsApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class WhatsAppOptionsBindingTests
{
    [Fact]
    public void Information_requested_template_binds_from_expected_tree()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsApp:Templates:InformationRequested:Name"] =
                    "resident_reply_required",
                ["WhatsApp:Templates:InformationRequested:Language"] = "pt_BR",
                ["WhatsApp:Templates:InformationRequested:BodyParameterName"] =
                    "resident_first_name"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<WhatsAppOptions>(configuration.GetSection(
            WhatsAppOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<WhatsAppOptions>>();

        Assert.Equal("WhatsApp", WhatsAppOptions.SectionName);
        Assert.Equal("resident_reply_required",
            options.Value.Templates.InformationRequested.Name);
        Assert.Equal("pt_BR",
            options.Value.Templates.InformationRequested.Language);
        Assert.Equal("resident_first_name",
            options.Value.Templates.InformationRequested.BodyParameterName);
    }

    [Fact]
    public void Manager_new_request_template_binds_from_expected_tree()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsApp:Templates:ManagerNewRequest:Name"] =
                    "manager_new_request",
                ["WhatsApp:Templates:ManagerNewRequest:Language"] = "pt_BR",
                ["WhatsApp:Templates:ManagerNewRequest:BodyParameterName"] =
                    "request_summary"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<WhatsAppOptions>(configuration.GetSection(
            WhatsAppOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var template = provider.GetRequiredService<IOptions<WhatsAppOptions>>()
            .Value.Templates.ManagerNewRequest;

        Assert.Equal("manager_new_request", template.Name);
        Assert.Equal("pt_BR", template.Language);
        Assert.Equal("request_summary", template.BodyParameterName);
    }

    [Fact]
    public void Scoped_consumers_resolve_the_same_options_and_value_instances()
    {
        var services = new ServiceCollection();
        services.AddOptions<WhatsAppOptions>().Configure(settings =>
        {
            settings.Templates.InformationRequested.Name = "message_warning";
            settings.Templates.InformationRequested.Language = "pt_BR";
        });
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider
            .GetRequiredService<IOptions<WhatsAppOptions>>();
        var second = secondScope.ServiceProvider
            .GetRequiredService<IOptions<WhatsAppOptions>>();

        Assert.Same(first, second);
        Assert.Same(first.Value, second.Value);
        Assert.Same(first.Value.Templates.InformationRequested,
            second.Value.Templates.InformationRequested);
    }
}
