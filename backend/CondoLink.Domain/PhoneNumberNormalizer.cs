namespace CondoLink.Domain;

public static class PhoneNumberNormalizer
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        if (trimmed.Any(character =>
                !char.IsAsciiDigit(character)
                && character is not '+' and not ' ' and not '-' and not '('
                    and not ')' and not '.' and not '/'))
            return null;

        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        var explicitlyInternational = trimmed.StartsWith('+');

        // Preserve the Brazilian international-dialling formats already accepted.
        if (!explicitlyInternational
            && digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits.Length >= 6
                && digits.AsSpan(4).StartsWith("55", StringComparison.Ordinal)
                    ? digits[4..]
                    : digits[2..];
            explicitlyInternational = true;
        }

        if (!explicitlyInternational)
        {
            if (digits.Length is not (10 or 11)) return null;
            digits = $"55{digits}";
        }

        if (digits.Length is < 8 or > 15 || digits[0] == '0') return null;

        if (digits.StartsWith("55", StringComparison.Ordinal))
        {
            if (digits.Length is not (12 or 13)) return null;

            // Legacy Brazilian mobiles have DDD + 8 digits and start in 6-9.
            // Their canonical cadastral representation includes the ninth digit.
            if (digits.Length == 12 && digits[4] is >= '6' and <= '9')
                digits = digits.Insert(4, "9");
        }

        return $"+{digits}";
    }

    public static bool IsValidOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) || Normalize(value) is not null;
}
