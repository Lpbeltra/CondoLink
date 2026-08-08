namespace CondoLink.Api.Features.WhatsApp;

public static class PhoneNumberNormalizer
{
    public static string? NormalizeBrazilian(string? value) =>
        Domain.BrazilianPhoneNumber.Normalize(value);

    public static string? NormalizeWhatsAppIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
        if (digits.Length is 10 or 11) digits = $"55{digits}";
        return digits.Length is >= 8 and <= 15 ? $"+{digits}" : null;
    }

    public static string[] IdentificationCandidates(string normalizedIdentifier)
    {
        return [normalizedIdentifier];
    }

    public static string Mask(string phoneNumber) =>
        phoneNumber.Length <= 4
            ? "****"
            : string.Concat("***", phoneNumber.AsSpan(phoneNumber.Length - 4));
}
