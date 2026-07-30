using Microsoft.AspNetCore.DataProtection;

namespace CondoLink.Api.Features.WhatsApp;

public interface IPhoneVerificationMessageProtector
{
    string Protect(string message);
    string Unprotect(string protectedMessage);
}

internal sealed class PhoneVerificationMessageProtector(
    IDataProtectionProvider provider)
    : IPhoneVerificationMessageProtector
{
    private readonly IDataProtector _protector =
        provider.CreateProtector("Comvy.WhatsApp.PhoneVerification.v1");

    public string Protect(string message) => _protector.Protect(message);

    public string Unprotect(string protectedMessage) =>
        _protector.Unprotect(protectedMessage);
}
