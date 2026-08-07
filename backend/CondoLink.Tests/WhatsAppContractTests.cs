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

    [Fact]
    public void Template_quick_reply_preserves_stable_id_and_visible_title()
    {
        using var document = JsonDocument.Parse("""
            {"entry":[{"changes":[{"value":{"messages":[{
              "from":"5511999990001","id":"wamid.reply","timestamp":"1785236400",
              "type":"button","context":{"id":"wamid.original-template"},
              "button":{"payload":"resident_reply_now","text":"Responder agora"}
            }]}}]}]}
            """);

        var message = Assert.Single(WhatsAppWebhookParser.Parse(document.RootElement));

        Assert.Equal("button", message.RawMessageType);
        Assert.Equal("quick_reply", message.ParsedMessageType);
        Assert.Equal("resident_reply_now", message.QuickReplyId);
        Assert.Equal("Responder agora", message.QuickReplyTitle);
        Assert.Equal("wamid.original-template", message.ReplyToExternalMessageId);
        Assert.True(message.HasButton);
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

    [Fact]
    public void Official_audio_payload_exposes_snake_case_download_metadata()
    {
        using var document = JsonDocument.Parse("""
            {
              "entry": [{
                "changes": [{
                  "value": {
                    "messages": [{
                      "from": "5511999990001",
                      "id": "wamid.audio-realistic",
                      "timestamp": "1785236400",
                      "type": "audio",
                      "audio": {
                        "id": "media-id",
                        "mime_type": "audio/ogg; codecs=opus",
                        "voice": true
                      }
                    }]
                  }
                }]
              }]
            }
            """);

        var message = Assert.Single(WhatsAppWebhookParser.Parse(document.RootElement));

        Assert.Equal("audio", message.MessageType);
        Assert.Equal("media-id", message.MediaId);
        Assert.Equal("audio/ogg; codecs=opus", message.MediaContentType);
        Assert.Null(message.FileName);
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

    [Fact]
    public void Legacy_brazilian_mobile_produces_only_the_official_ninth_digit_variant()
    {
        Assert.Equal(
            ["+554497562161", "+5544997562161"],
            PhoneNumberNormalizer.IdentificationCandidates("+554497562161"));
    }

    [Fact]
    public void Official_brazilian_mobile_produces_the_inverse_legacy_variant()
    {
        Assert.Equal(
            ["+5544997562161", "+554497562161"],
            PhoneNumberNormalizer.IdentificationCandidates("+5544997562161"));
    }

    [Fact]
    public void Foreign_number_is_not_transformed()
    {
        Assert.Equal(
            ["+14155552671"],
            PhoneNumberNormalizer.IdentificationCandidates("+14155552671"));
    }

    private static JsonDocument ReadFixture(string name)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "WhatsApp", name);
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
