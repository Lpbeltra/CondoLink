using System.Net;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CondoLink.Tests;

public sealed class MetaWhatsAppClientTests
{
    [Fact]
    public async Task Information_request_template_payload_has_name_language_name_and_buttons()
    {
        var handler = new RecordingHandler();
        var client = new MetaWhatsAppClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.facebook.com/")
        }, Options.Create(new WhatsAppOptions
        {
            Enabled = true,
            PhoneNumberId = "phone-id",
            AccessToken = "secret"
        }), NullLogger<MetaWhatsAppClient>.Instance);

        var result = await client.SendTemplateAsync("+5511999990001",
            "message_warning", "pt_BR", ["Ana"],
            ["resident_reply_now", "resident_reply_later"],
            CancellationToken.None);

        Assert.True(result.Succeeded);
        using var json = JsonDocument.Parse(handler.Body!);
        var root = json.RootElement;
        Assert.Equal("template", root.GetProperty("type").GetString());
        var template = root.GetProperty("template");
        Assert.Equal("message_warning", template.GetProperty("name").GetString());
        Assert.Equal("pt_BR", template.GetProperty("language")
            .GetProperty("code").GetString());
        var components = template.GetProperty("components");
        Assert.Equal(3, components.GetArrayLength());
        Assert.Equal("Ana", components[0].GetProperty("parameters")[0]
            .GetProperty("text").GetString());
        Assert.Equal("resident_reply_now", components[1]
            .GetProperty("parameters")[0].GetProperty("payload").GetString());
        Assert.Equal("resident_reply_later", components[2]
            .GetProperty("parameters")[0].GetProperty("payload").GetString());
    }

    [Theory]
    [InlineData("Ana Maria", "Ana")]
    [InlineData("  João  Silva  ", "João")]
    [InlineData("", "Morador")]
    public void Safe_first_name_returns_only_a_display_safe_value(
        string fullName, string expected) =>
        Assert.Equal(expected, WhatsAppOutboundWorker.SafeFirstName(fullName));

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"messages\":[{\"id\":\"wamid.sent\"}]}",
                    Encoding.UTF8, "application/json")
            };
        }
    }
}
