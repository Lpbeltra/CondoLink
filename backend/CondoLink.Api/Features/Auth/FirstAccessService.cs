using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using CondoLink.Infrastructure.Identity;

namespace CondoLink.Api.Features.Auth;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Comvy";
    public bool UseSsl { get; set; } = true;
}

public sealed class FirstAccessOptions
{
    public const string SectionName = "FirstAccess";
    public string FrontendBaseUrl { get; set; } = "";
}

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string html, CancellationToken cancellationToken);
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string html, CancellationToken cancellationToken)
    {
        var value = options.Value;
        if (!value.Enabled) throw new InvalidOperationException("O envio de e-mail está desabilitado.");
        if (string.IsNullOrWhiteSpace(value.Host) || string.IsNullOrWhiteSpace(value.FromAddress))
            throw new InvalidOperationException("A configuração de e-mail está incompleta.");
        using var client = new SmtpClient(value.Host, value.Port)
        {
            EnableSsl = value.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(value.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(value.Username, value.Password)
        };
        using var message = new MailMessage
        {
            From = new MailAddress(value.FromAddress, value.FromName),
            Subject = subject,
            Body = html,
            IsBodyHtml = true
        };
        message.To.Add(recipient);
        await client.SendMailAsync(message, cancellationToken);
    }
}

public sealed class FirstAccessService(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IOptions<FirstAccessOptions> options,
    ILogger<FirstAccessService> logger)
{
    public async Task<string> CreateLinkAsync(ApplicationUser user)
    {
        if (!user.MustChangePassword) throw new InvalidOperationException("O primeiro acesso já foi concluído.");
        var baseUrl = options.Value.FrontendBaseUrl.TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("FirstAccess__FrontendBaseUrl deve ser uma URL HTTPS válida.");
        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
            throw new InvalidOperationException("Não foi possível gerar um novo link de primeiro acesso.");
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        return $"{baseUrl}/primeiro-acesso?userId={Uri.EscapeDataString(user.Id.ToString())}&token={Uri.EscapeDataString(token)}";
    }

    public async Task<bool> SendAsync(ApplicationUser user, string condominiumName, CancellationToken cancellationToken)
    {
        if (!user.EmailDeliveryEnabled || !user.MustChangePassword) return false;
        try
        {
            var link = await CreateLinkAsync(user);
            var name = WebUtility.HtmlEncode(user.FullName);
            var condominium = WebUtility.HtmlEncode(condominiumName);
            var safeLink = WebUtility.HtmlEncode(link);
            var html = $"<p>Olá, {name}.</p><p>A administração do {condominium} criou seu acesso ao Comvy.</p><p><a href=\"{safeLink}\">Criar minha senha</a></p><p>Este link é válido por 24 horas.</p>";
            await emailSender.SendAsync(user.Email!, "Seu acesso ao Comvy foi criado", html, cancellationToken);
            user.MarkFirstAccessInviteSent(DateTime.UtcNow);
            await userManager.UpdateAsync(user);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "First-access email failed for UserId {UserId}.", user.Id);
            user.MarkFirstAccessInviteFailed(DateTime.UtcNow);
            await userManager.UpdateAsync(user);
            return false;
        }
    }
}
