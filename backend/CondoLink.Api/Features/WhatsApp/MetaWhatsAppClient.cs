using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

    public Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneNumber,
        string templateName,
        string language,
        IReadOnlyList<string> bodyParameters,
        IReadOnlyList<string> quickReplyPayloads,
        CancellationToken cancellationToken,
        string? bodyParameterName = null) =>
        SendTemplateAsync(phoneNumber, templateName, language, bodyParameters,
            quickReplyPayloads, cancellationToken, bodyParameterName, []);

    public async Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneNumber,
        string templateName,
        string language,
        IReadOnlyList<string> bodyParameters,
        IReadOnlyList<string> quickReplyPayloads,
        CancellationToken cancellationToken,
        string? bodyParameterName = null,
        IReadOnlyList<string> urlButtonParameters)
    {
        var stage = "building_payload";
        int? httpStatus = null;
        HttpResponseMessage? response = null;
        var settings = options.Value;
        var namedParameterEnabled = !string.IsNullOrWhiteSpace(bodyParameterName);
        try
        {
            if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.PhoneNumberId)
                || string.IsNullOrWhiteSpace(settings.AccessToken))
                return LogAndReturnFailure(PermanentConfigurationFailure(
                    "whatsapp_not_configured",
                    "WhatsApp integration is not configured.", stage),
                    templateName, language, namedParameterEnabled);
            if (string.IsNullOrWhiteSpace(templateName)
                || templateName.Any(char.IsWhiteSpace))
                return LogAndReturnFailure(PermanentConfigurationFailure(
                    "template_name_invalid",
                    "Template name is empty or contains whitespace.", stage),
                    templateName, language, namedParameterEnabled);
            if (string.IsNullOrWhiteSpace(language)
                || language.Any(char.IsWhiteSpace))
                return LogAndReturnFailure(PermanentConfigurationFailure(
                    "template_language_invalid",
                    "Template language is empty or contains whitespace.", stage),
                    templateName, language, namedParameterEnabled);

            var components = new List<object>();
            if (bodyParameters.Count > 0)
                components.Add(new
                {
                    type = "body",
                    parameters = bodyParameters.Select(value =>
                        namedParameterEnabled
                            ? (object)new
                            {
                                type = "text",
                                parameter_name = bodyParameterName!.Trim(),
                                text = value
                            }
                            : new { type = "text", text = value }).ToArray()
                });
            for (var index = 0; index < quickReplyPayloads.Count; index++)
                components.Add(new
                {
                    type = "button",
                    sub_type = "quick_reply",
                    index = index.ToString(),
                    parameters = new[] { new
                    {
                        type = "payload",
                        payload = quickReplyPayloads[index]
                    } }
                });
            for (var index = 0; index < urlButtonParameters.Count; index++)
                components.Add(new
                {
                    type = "button",
                    sub_type = "url",
                    index = index.ToString(),
                    parameters = new[] { new
                    {
                        type = "text",
                        text = urlButtonParameters[index]
                    } }
                });
            var payload = new
            {
                messaging_product = "whatsapp",
                to = phoneNumber.TrimStart('+'),
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = language },
                    components = components.ToArray()
                }
            };
            LogTemplateEvent(LogLevel.Information, templateName, language,
                namedParameterEnabled, httpStatus, null, stage);

            stage = "serializing_payload";
            var json = SerializePayload(payload);
            stage = "creating_request";
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{settings.ApiVersion}/{settings.PhoneNumberId}/messages");
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.AccessToken);
            request.Content = new StringContent(json, Encoding.UTF8,
                "application/json");

            stage = "sending_http";
            response = await httpClient.SendAsync(request, cancellationToken);
            stage = "receiving_response";
            httpStatus = (int)response.StatusCode;
            stage = "reading_response";
            var responseBody = await response.Content
                .ReadAsStringAsync(cancellationToken);
            stage = "parsing_response";
            var result = response.IsSuccessStatusCode
                ? ParseTemplateSuccess(responseBody, httpStatus.Value)
                : ParseMetaFailure(responseBody, httpStatus.Value,
                    bodyParameters);
            stage = "completed";
            LogTemplateEvent(result.Succeeded ? LogLevel.Information :
                result.IsTransient ? LogLevel.Warning : LogLevel.Error,
                templateName, language, namedParameterEnabled, httpStatus,
                result.ErrorCode, stage);
            return result;
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            LogTemplateException(exception, templateName, language, stage,
                httpStatus, namedParameterEnabled);
            return new(false, null, "Provider request timed out.", true,
                "timeout", httpStatus, FailureKind: "Timeout",
                FailureStage: stage);
        }
        catch (HttpRequestException exception)
        {
            httpStatus ??= exception.StatusCode is null
                ? null : (int)exception.StatusCode.Value;
            LogTemplateException(exception, templateName, language, stage,
                httpStatus, namedParameterEnabled);
            var transient = !httpStatus.HasValue
                || IsTransientStatus(httpStatus.Value);
            return new(false, null, "Provider HTTP request failed.", transient,
                httpStatus.HasValue ? $"http_{httpStatus}" : "network",
                httpStatus, FailureKind: "Transport",
                FailureStage: stage);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested)
        {
            LogTemplateException(exception, templateName, language, stage,
                httpStatus, namedParameterEnabled);
            throw;
        }
        catch (Exception exception)
        {
            LogTemplateException(exception, templateName, language, stage,
                httpStatus, namedParameterEnabled);
            var transient = exception is IOException;
            return new(false, null,
                $"Template send failed during {stage} ({exception.GetType().Name}).",
                transient, transient ? "io_error" : "client_error", httpStatus,
                FailureKind: FailureKindFor(stage, exception),
                FailureStage: stage);
        }
        finally
        {
            response?.Dispose();
        }
    }

    private void LogTemplateException(Exception exception, string templateName,
        string language, string stage, int? httpStatus,
        bool namedParameterEnabled) =>
        LogTemplateEvent(LogLevel.Error, templateName, language,
            namedParameterEnabled, httpStatus, null, stage);

    internal static string SerializePayload(object payload) =>
        JsonSerializer.Serialize(payload);

    private static WhatsAppSendResult PermanentConfigurationFailure(
        string code, string description, string stage) =>
        new(false, null, description, false, code,
            FailureKind: "Configuration", FailureStage: stage);

    private WhatsAppSendResult LogAndReturnFailure(WhatsAppSendResult result,
        string templateName, string language, bool namedParameterEnabled)
    {
        LogTemplateEvent(LogLevel.Error, templateName, language,
            namedParameterEnabled, result.HttpStatusCode, result.ErrorCode,
            result.FailureStage);
        return result;
    }

    private void LogTemplateEvent(LogLevel level, string templateName,
        string language, bool namedParameterEnabled, int? httpStatus,
        string? metaErrorCode, string? failureStage) =>
        logger.Log(level,
            "WhatsApp template. TemplateName: {TemplateName}; Language: {Language}; NamedParameterEnabled: {NamedParameterEnabled}; HttpStatus: {HttpStatus}; MetaErrorCode: {MetaErrorCode}; FailureStage: {FailureStage}.",
            templateName, language, namedParameterEnabled, httpStatus,
            metaErrorCode, failureStage);

    private static string FailureKindFor(string stage, Exception exception) =>
        exception is IOException ? "TransportIO"
        : stage == "serializing_payload" ? "Serialization"
        : stage is "reading_response" or "parsing_response" ? "ProviderResponse"
        : "Client";

    private static WhatsAppSendResult ParseTemplateSuccess(
        string responseBody, int httpStatus)
    {
        using var document = JsonDocument.Parse(responseBody);
        var id = document.RootElement.TryGetProperty("messages", out var messages)
            && messages.ValueKind == JsonValueKind.Array
            && messages.GetArrayLength() > 0
            && messages[0].TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;
        return string.IsNullOrWhiteSpace(id)
            ? new(false, null, "Provider success response did not contain a message id.",
                false, "invalid_provider_response", httpStatus,
                FailureKind: "ProviderResponse", FailureStage: "parsing_response")
            : new(true, id, null, false, null, httpStatus);
    }

    private static WhatsAppSendResult ParseMetaFailure(
        string responseBody, int httpStatus,
        IReadOnlyList<string> sensitiveValues)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            var error = root.TryGetProperty("error", out var value)
                ? value : default;
            var type = StringProperty(error, "type");
            var code = ScalarProperty(error, "code")
                ?? $"http_{httpStatus}";
            var subcode = ScalarProperty(error, "error_subcode");
            var details = error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("error_data", out var errorData)
                ? StringProperty(errorData, "details") : null;
            var safeDetails = SafeTechnicalDetails(details, sensitiveValues);
            var description = $"Meta HTTP {httpStatus}; type={type ?? "unknown"}; code={code}"
                + (subcode is null ? string.Empty : $"; subcode={subcode}")
                + (safeDetails is null ? string.Empty : $"; details={safeDetails}");
            return new(false, null, description, IsTransientStatus(httpStatus),
                code, httpStatus, type, subcode, "MetaApi",
                "receiving_response");
        }
        catch (JsonException)
        {
            return new(false, null,
                $"Meta HTTP {httpStatus}; response error was not valid JSON.",
                IsTransientStatus(httpStatus), $"http_{httpStatus}", httpStatus,
                FailureKind: "ProviderResponse",
                FailureStage: "parsing_response");
        }
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? ScalarProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? value.ToString() : null;

    private static bool IsTransientStatus(int status) =>
        status is 408 or 429 or >= 500;

    internal static string? SafeTechnicalDetails(string? details,
        IReadOnlyList<string>? sensitiveValues = null)
    {
        if (string.IsNullOrWhiteSpace(details)) return null;
        var value = details.Trim();
        if (value.Length > 300 || value.Contains('@') || value.Contains('+'))
            return null;
        if (sensitiveValues?.Any(sensitive =>
                !string.IsNullOrWhiteSpace(sensitive)
                && value.Contains(sensitive, StringComparison.OrdinalIgnoreCase)) == true)
            return null;
        var consecutiveDigits = 0;
        foreach (var character in value)
        {
            consecutiveDigits = char.IsDigit(character)
                ? consecutiveDigits + 1 : 0;
            if (consecutiveDigits >= 5) return null;
        }
        return value;
    }

    private static async Task<string?> ProviderErrorCodeAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream,
                cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("error", out var error)
                ? ScalarProperty(error, "code") : null;
        }
        catch (JsonException)
        {
            return null;
        }
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
                var errorCode = await ProviderErrorCodeAsync(
                    response, cancellationToken)
                    ?? ((int)response.StatusCode).ToString();
                logger.LogWarning(
                    "WhatsApp send failed with HTTP {StatusCode}; ErrorCode: {ErrorCode}.",
                    (int)response.StatusCode, errorCode);
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
            logger.LogInformation(
                "WhatsApp send accepted with HTTP {StatusCode}.",
                (int)response.StatusCode);
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
