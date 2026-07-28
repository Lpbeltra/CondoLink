using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;

namespace CondoLink.Tests;

public sealed class WhatsAppContractTests
{
    [Theory]
    [InlineData("text-message.json", "text", "Menu")]
    [InlineData("interactive-reply.json", "interactive", "1")]
    public void Supported_provider_payload_is_normalized(
        string fixture,
        string expectedType,
        string expectedText)
    {
        using var document = ReadFixture(fixture);
        var message = Assert.Single(
            WhatsAppWebhookParser.Parse(document.RootElement));
        Assert.Equal(expectedType, message.MessageType);
        Assert.Equal(expectedText, message.Text);
        Assert.StartsWith("wamid.", message.ExternalMessageId);
    }

    [Theory]
    [InlineData("status-event.json")]
    [InlineData("no-messages.json")]
    public void Technical_payload_without_inbound_message_is_ignored(string fixture)
    {
        using var document = ReadFixture(fixture);
        Assert.Empty(WhatsAppWebhookParser.Parse(document.RootElement));
    }

    [Theory]
    [InlineData("image-message.json", "image", "media-image-1", null)]
    [InlineData("document-message.json", "document", "media-document-1", "documento.pdf")]
    public void Media_payload_exposes_download_metadata(
        string fixture,
        string type,
        string mediaId,
        string? fileName)
    {
        using var document = ReadFixture(fixture);
        var message = Assert.Single(WhatsAppWebhookParser.Parse(document.RootElement));
        Assert.Equal(type, message.MessageType);
        Assert.Equal(mediaId, message.MediaId);
        Assert.Equal(fileName, message.FileName);
    }

    [Theory]
    [InlineData("11999990001", "+5511999990001")]
    [InlineData("(11) 99999-0001", "+5511999990001")]
    [InlineData("+55 11 99999-0001", "+5511999990001")]
    [InlineData("00115511999990001", "+5511999990001")]
    [InlineData("99990001", null)]
    public void Brazilian_phone_normalization_is_canonical_and_safe(
        string input,
        string? expected) =>
        Assert.Equal(expected, PhoneNumberNormalizer.NormalizeBrazilian(input));

    private static JsonDocument ReadFixture(string name)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "WhatsApp", name);
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
