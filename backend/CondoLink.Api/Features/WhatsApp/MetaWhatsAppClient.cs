using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class MetaWhatsAppClient(
    HttpClient httpClient,
    IOptions<WhatsAppOptions> options,
    ILogger<MetaWhatsAppClient> logger) : IWhatsAppClient
{
    private const int MaximumMediaBytes = 15 * 1024 * 1024;
    public async Task<WhatsAppSendResult> SendTextAsync(
        string phoneNumber,
        string text,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return new(false, null, "WhatsApp integration is disabled.");
        if (string.IsNullOrWhiteSpace(settings.PhoneNumberId)
            || string.IsNullOrWhiteSpace(settings.AccessToken))
            return new(false, null, "WhatsApp integration is not configured.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{settings.ApiVersion}/{settings.PhoneNumberId}/messages");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        request.Content = JsonContent.Create(new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = phoneNumber.TrimStart('+'),
            type = "text",
            text = new { preview_url = false, body = text }
        });

        return await SendAsync(request, cancellationToken);
    }

    public async Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneNumber,
        string templateName,
        string language,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.PhoneNumberId)
            || string.IsNullOrWhiteSpace(settings.AccessToken))
            return new(false, null, "WhatsApp integration is not configured.");
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{settings.ApiVersion}/{settings.PhoneNumberId}/messages");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        request.Content = JsonContent.Create(new
        {
            messaging_product = "whatsapp",
            to = phoneNumber.TrimStart('+'),
            type = "template",
            template = new { name = templateName, language = new { code = language } }
        });
        return await SendAsync(request, cancellationToken);
    }

    private async Task<WhatsAppSendResult> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorCode = ((int)response.StatusCode).ToString();
                logger.LogWarning(
                    "WhatsApp send failed with HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return new(false, null,
                    $"Provider returned HTTP {(int)response.StatusCode}.",
                    (int)response.StatusCode is 408 or 429 or >= 500,
                    errorCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var id = document.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var idElement)
                    ? idElement.GetString()
                    : null;
            return new(true, id, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("WhatsApp send timed out.");
            return new(false, null, "Provider request timed out.", true, "timeout");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "WhatsApp send failed.");
            return new(false, null, "Provider request failed.", true, "network");
        }
    }

    public async Task<WhatsAppMediaResult> DownloadMediaAsync(
        string mediaId,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.AccessToken))
            return new(false, null, null, "WhatsApp integration is not configured.");
        try
        {
            using var metadataRequest = AuthorizedGet(
                $"{settings.ApiVersion}/{Uri.EscapeDataString(mediaId)}", settings.AccessToken);
            using var metadataResponse =
                await httpClient.SendAsync(metadataRequest, cancellationToken);
            if (!metadataResponse.IsSuccessStatusCode)
                return new(false, null, null, "Media metadata was not found.");
            await using var metadataStream =
                await metadataResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var metadata =
                await JsonDocument.ParseAsync(metadataStream, cancellationToken: cancellationToken);
            if (!metadata.RootElement.TryGetProperty("url", out var urlElement)
                || !Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var url))
                return new(false, null, null, "Provider returned invalid media metadata.");

            using var downloadRequest = AuthorizedGet(url.ToString(), settings.AccessToken);
            using var downloadResponse =
                await httpClient.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!downloadResponse.IsSuccessStatusCode)
                return new(false, null, null, "Media download failed.");
            if (downloadResponse.Content.Headers.ContentLength > MaximumMediaBytes)
                return new(false, null, null, "Media exceeds 15 MB.");
            var contentType = downloadResponse.Content.Headers.ContentType?.MediaType;
            await using var input =
                await downloadResponse.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (output.Length + read > MaximumMediaBytes)
                    return new(false, null, null, "Media exceeds 15 MB.");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            return new(true, output.ToArray(), contentType, null);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException
            || exception is OperationCanceledException
                && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "WhatsApp media download failed.");
            return new(false, null, null, "Media download failed.");
        }
    }

    private static HttpRequestMessage AuthorizedGet(string url, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
