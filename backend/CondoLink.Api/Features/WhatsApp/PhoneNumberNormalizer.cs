namespace CondoLink.Api.Features.WhatsApp;

public static class PhoneNumberNormalizer
{
    public static string? NormalizeBrazilian(string? value) =>
        Domain.PhoneNumberNormalizer.Normalize(value);

    public static string? NormalizeWhatsAppIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
        return digits.Length is >= 8 and <= 15 && digits[0] != '0'
            ? $"+{digits}"
            : null;
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
