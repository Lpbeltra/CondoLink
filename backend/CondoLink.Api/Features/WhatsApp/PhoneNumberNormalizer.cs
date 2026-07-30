namespace CondoLink.Api.Features.WhatsApp;

public static class PhoneNumberNormalizer
{
    public static string? NormalizeBrazilian(string? value) =>
        Domain.BrazilianPhoneNumber.Normalize(value);

    public static string Mask(string phoneNumber) =>
        phoneNumber.Length <= 4
            ? "****"
            : string.Concat("***", phoneNumber.AsSpan(phoneNumber.Length - 4));
}
