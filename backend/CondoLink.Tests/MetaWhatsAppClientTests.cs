using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.WhatsApp;
using Microsoft.Extensions.Logging;
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
        Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
        Assert.Equal("5511999990001", root.GetProperty("to").GetString());
        Assert.Equal("template", root.GetProperty("type").GetString());
        var template = root.GetProperty("template");
        Assert.Equal("message_warning", template.GetProperty("name").GetString());
        Assert.Equal("pt_BR", template.GetProperty("language")
            .GetProperty("code").GetString());
        var components = template.GetProperty("components");
        Assert.Equal(3, components.GetArrayLength());
        var bodyParameters = components[0].GetProperty("parameters");
        Assert.Single(bodyParameters.EnumerateArray());
        Assert.Equal("Ana", bodyParameters[0]
            .GetProperty("text").GetString());
        Assert.False(bodyParameters[0].TryGetProperty(
            "parameter_name", out _));
        Assert.Equal("button", components[1].GetProperty("type").GetString());
        Assert.Equal("quick_reply", components[1].GetProperty("sub_type").GetString());
        Assert.Equal("0", components[1].GetProperty("index").GetString());
        Assert.Equal("resident_reply_now", components[1]
            .GetProperty("parameters")[0].GetProperty("payload").GetString());
        Assert.Equal("button", components[2].GetProperty("type").GetString());
        Assert.Equal("quick_reply", components[2].GetProperty("sub_type").GetString());
        Assert.Equal("1", components[2].GetProperty("index").GetString());
        Assert.Equal("resident_reply_later", components[2]
            .GetProperty("parameters")[0].GetProperty("payload").GetString());
    }

    [Fact]
    public async Task Named_body_parameter_uses_parameter_name_and_preserves_buttons()
    {
        var handler = new RecordingHandler();

        var result = await NewClient(handler).SendTemplateAsync(
            "+5511999990001", "resident_reply_required", "pt_BR", ["Ana"],
            ["resident_reply_now", "resident_reply_later"], default,
            "resident_first_name");

        Assert.True(result.Succeeded);
        using var json = JsonDocument.Parse(handler.Body!);
        var components = json.RootElement.GetProperty("template")
            .GetProperty("components");
        var parameter = components[0].GetProperty("parameters")[0];
        Assert.Equal("text", parameter.GetProperty("type").GetString());
        Assert.Equal("resident_first_name", parameter
            .GetProperty("parameter_name").GetString());
        Assert.Equal("Ana", parameter.GetProperty("text").GetString());
        Assert.Equal("0", components[1].GetProperty("index").GetString());
        Assert.Equal("resident_reply_now", components[1]
            .GetProperty("parameters")[0].GetProperty("payload").GetString());
        Assert.Equal("1", components[2].GetProperty("index").GetString());
        Assert.Equal("resident_reply_later", components[2]
            .GetProperty("parameters")[0].GetProperty("payload").GetString());
    }

    [Theory]
    [InlineData(400, "OAuthException", "132001", "Template does not exist", false)]
    [InlineData(400, "OAuthException", "132012", "Template parameter format mismatch", false)]
    [InlineData(400, "GraphMethodException", "100", "Invalid button component", false)]
    [InlineData(429, "OAuthException", "4", "Rate limit reached", true)]
    [InlineData(500, "OAuthException", "2", "Temporary service failure", true)]
    public async Task Meta_error_is_parsed_and_classified(
        int status, string type, string code, string details, bool transient)
    {
        var body = JsonSerializer.Serialize(new
        {
            error = new
            {
                message = "not persisted",
                type,
                code = int.Parse(code),
                error_subcode = 2494010,
                error_data = new { details },
                fbtrace_id = "trace"
            }
        });
        var handler = new RecordingHandler((HttpStatusCode)status, body);
        var result = await NewClient(handler).SendTemplateAsync(
            "+5511999990001", "message_warning", "pt_BR", ["Ana"],
            ["resident_reply_now", "resident_reply_later"], default);

        Assert.False(result.Succeeded);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(type, result.ErrorType);
        Assert.Equal("2494010", result.ErrorSubcode);
        Assert.Equal(status, result.HttpStatusCode);
        Assert.Equal(transient, result.IsTransient);
        Assert.Equal("MetaApi", result.FailureKind);
        Assert.Equal("receiving_response", result.FailureStage);
        Assert.Contains(details, result.Error);
        Assert.DoesNotContain("not persisted", result.Error);
    }

    [Theory]
    [InlineData("message warning", "pt_BR", "template_name_invalid")]
    [InlineData("message_warning", "pt BR", "template_language_invalid")]
    [InlineData("", "pt_BR", "template_name_invalid")]
    public async Task Invalid_template_configuration_fails_before_http(
        string name, string language, string expectedCode)
    {
        var handler = new RecordingHandler();
        var result = await NewClient(handler).SendTemplateAsync(
            "+5511999990001", name, language, ["Ana"], [], default);

        Assert.False(result.Succeeded);
        Assert.False(result.IsTransient);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal("Configuration", result.FailureKind);
        Assert.Equal("building_payload", result.FailureStage);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Configuration_failure_is_logged_before_returning()
    {
        var handler = new RecordingHandler();
        var logger = new RecordingLogger<MetaWhatsAppClient>();

        var result = await NewClient(handler, logger).SendTemplateAsync(
            "+5511999990001", "message warning", "pt_BR", ["Ana"], [], default);

        Assert.False(result.Succeeded);
        Assert.Contains(logger.Messages, message =>
            message.Contains("MetaErrorCode: template_name_invalid", StringComparison.Ordinal)
            && message.Contains("FailureStage: building_payload", StringComparison.Ordinal)
            && message.Contains("NamedParameterEnabled: False", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Timeout_is_transient()
    {
        var handler = new RecordingHandler(exception:
            new TaskCanceledException("timeout"));
        var logger = new RecordingLogger<MetaWhatsAppClient>();
        var result = await NewClient(handler, logger).SendTemplateAsync(
            "+5511999990001", "message_warning", "pt_BR", ["Ana"], [], default);
        Assert.Equal("timeout", result.ErrorCode);
        Assert.True(result.IsTransient);
        Assert.Equal("Timeout", result.FailureKind);
        Assert.Equal("sending_http", result.FailureStage);
        Assert.Contains(logger.Messages, message =>
            message.Contains("FailureStage: sending_http", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Http_request_exception_is_transient()
    {
        var handler = new RecordingHandler(exception:
            new HttpRequestException("network"));
        var result = await NewClient(handler).SendTemplateAsync(
            "+5511999990001", "message_warning", "pt_BR", ["Ana"], [], default);
        Assert.Equal("network", result.ErrorCode);
        Assert.True(result.IsTransient);
        Assert.Equal("Transport", result.FailureKind);
        Assert.Equal("sending_http", result.FailureStage);
    }

    [Fact]
    public async Task Http_request_with_socket_exception_logs_only_safe_fields()
    {
        var socket = new SocketException((int)SocketError.ConnectionRefused);
        var handler = new RecordingHandler(exception:
            new HttpRequestException("network", socket));
        var logger = new RecordingLogger<MetaWhatsAppClient>();

        var result = await NewClient(handler, logger).SendTemplateAsync(
            "+5511999990001", "message_warning", "pt_BR", ["Ana"], [], default);

        Assert.True(result.IsTransient);
        Assert.Equal("Transport", result.FailureKind);
        Assert.Equal("sending_http", result.FailureStage);
        var logs = string.Join("\n", logger.Messages);
        Assert.Contains("FailureStage: sending_http", logs);
        Assert.DoesNotContain(SocketError.ConnectionRefused.ToString(), logs);
        Assert.DoesNotContain(socket.NativeErrorCode.ToString(), logs);
    }

    [Fact]
    public async Task IOException_during_send_is_transient_and_diagnosed()
    {
        var handler = new RecordingHandler(exception: new IOException("io"));
        var logger = new RecordingLogger<MetaWhatsAppClient>();

        var result = await NewClient(handler, logger).SendTemplateAsync(
            "+5511999990001", "message_warning", "pt_BR", ["Ana"], [], default);

        Assert.True(result.IsTransient);
        Assert.Equal("io_error", result.ErrorCode);
        Assert.Equal("TransportIO", result.FailureKind);
        Assert.Equal("sending_http", result.FailureStage);
        Assert.Contains(logger.Messages, message =>
            message.Contains("FailureStage: sending_http", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invalid_success_json_is_a_permanent_parsing_failure()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "not-json");
        var result = await NewClient(handler).SendTemplateAsync(
            "+5511999990001", "message_warning", "pt_BR", ["Ana"], [], default);

        Assert.False(result.Succeeded);
        Assert.False(result.IsTransient);
        Assert.Equal("client_error", result.ErrorCode);
        Assert.Equal("ProviderResponse", result.FailureKind);
        Assert.Equal("parsing_response", result.FailureStage);
        Assert.Contains("parsing_response", result.Error);
        Assert.Contains("Json", result.Error);
    }

    [Fact]
    public async Task Invalid_meta_error_json_keeps_safe_http_diagnostic()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadRequest, "not-json");
        var result = await NewClient(handler).SendTemplateAsync(
            "+5511999990001", "message_warning", "pt_BR", ["Ana"], [], default);

        Assert.False(result.Succeeded);
        Assert.False(result.IsTransient);
        Assert.Equal("http_400", result.ErrorCode);
        Assert.Equal(400, result.HttpStatusCode);
        Assert.Equal("Meta HTTP 400; response error was not valid JSON.", result.Error);
    }

    [Fact]
    public async Task Diagnostics_do_not_log_secrets_or_personal_values()
    {
        const string token = "top-secret-token";
        const string phone = "+5511987654321";
        const string name = "SensitiveResidentName";
        var logger = new RecordingLogger<MetaWhatsAppClient>();
        var handler = new RecordingHandler(HttpStatusCode.BadRequest,
            "{\"error\":{\"type\":\"OAuthException\",\"code\":132001}} ");
        var options = Options.Create(new WhatsAppOptions
        {
            Enabled = true, PhoneNumberId = "phone-id", AccessToken = token
        });
        var client = new MetaWhatsAppClient(new HttpClient(handler)
            { BaseAddress = new Uri("https://graph.facebook.com/") }, options, logger);

        await client.SendTemplateAsync(phone, "message_warning", "pt_BR",
            [name], ["resident_reply_now"], default);

        var logs = string.Join("\n", logger.Messages);
        Assert.DoesNotContain(token, logs);
        Assert.DoesNotContain(phone, logs);
        Assert.DoesNotContain(phone.TrimStart('+'), logs);
        Assert.DoesNotContain(name, logs);
        Assert.DoesNotContain(handler.Body!, logs);
        Assert.Contains("completed", logs);
    }

    [Fact]
    public void Personal_details_are_not_retained()
    {
        Assert.Null(MetaWhatsAppClient.SafeTechnicalDetails(
            "Invalid value for Ana at +5511987654321"));
        Assert.Null(MetaWhatsAppClient.SafeTechnicalDetails(
            "Invalid value for Ana", ["Ana"]));
        Assert.Equal("Invalid button component",
            MetaWhatsAppClient.SafeTechnicalDetails("Invalid button component"));
    }

    [Fact]
    public void Serialization_failure_is_exposed_by_the_same_payload_serializer()
    {
        var cyclic = new CyclicPayload();
        cyclic.Self = cyclic;
        Assert.Throws<JsonException>(() =>
            MetaWhatsAppClient.SerializePayload(cyclic));
    }

    [Theory]
    [InlineData("Ana Maria", "Ana")]
    [InlineData("  João  Silva  ", "João")]
    [InlineData("", "Morador")]
    public void Safe_first_name_returns_only_a_display_safe_value(
        string fullName, string expected) =>
        Assert.Equal(expected, WhatsAppOutboundWorker.SafeFirstName(fullName));

    private static MetaWhatsAppClient NewClient(RecordingHandler handler,
        ILogger<MetaWhatsAppClient>? logger = null) =>
        new(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.facebook.com/")
        }, Options.Create(new WhatsAppOptions
        {
            Enabled = true,
            PhoneNumberId = "phone-id",
            AccessToken = "secret"
        }), logger ?? NullLogger<MetaWhatsAppClient>.Instance);

    private sealed class RecordingHandler(
        HttpStatusCode status = HttpStatusCode.OK,
        string body = "{\"messages\":[{\"id\":\"wamid.sent\"}]}",
        Exception? exception = null) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (exception is not null) throw exception;
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }

    private sealed class CyclicPayload
    {
        public CyclicPayload? Self { get; set; }
    }
}
