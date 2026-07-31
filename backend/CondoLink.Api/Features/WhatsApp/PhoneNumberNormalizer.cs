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
        var candidates = new List<string> { normalizedIdentifier };
        if (!normalizedIdentifier.StartsWith("+55", StringComparison.Ordinal))
            return [.. candidates];

        // +55 + DDD + 8 digits: only legacy mobile ranges are eligible.
        if (normalizedIdentifier.Length == 13
            && normalizedIdentifier[5] is >= '6' and <= '9')
            candidates.Add(normalizedIdentifier.Insert(5, "9"));
        // Inverse comparison is restricted to the official mobile ninth digit.
        else if (normalizedIdentifier.Length == 14
            && normalizedIdentifier[5] == '9'
            && normalizedIdentifier[6] is >= '6' and <= '9')
            candidates.Add(normalizedIdentifier.Remove(5, 1));

        return [.. candidates.Distinct(StringComparer.Ordinal)];
    }

    public static string Mask(string phoneNumber) =>
        phoneNumber.Length <= 4
            ? "****"
            : string.Concat("***", phoneNumber.AsSpan(phoneNumber.Length - 4));
}
