namespace CondoLink.Domain;

public static class BrazilianPhoneNumber
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits.Length >= 6
                && digits.AsSpan(4).StartsWith("55", StringComparison.Ordinal)
                    ? digits[4..]
                    : digits[2..];
        }

        if (digits.Length is 10 or 11) digits = $"55{digits}";
        if (!digits.StartsWith("55", StringComparison.Ordinal)
            || digits.Length is not (12 or 13))
            return null;

        return $"+{digits}";
    }

    public static bool IsValidOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) || Normalize(value) is not null;
}
