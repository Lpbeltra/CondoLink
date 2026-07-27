namespace CondoLink.Domain;

public static class RegistrationData
{
    private static readonly HashSet<string> States =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
        "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
        "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    ];

    public static string? Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return new string(value.Where(char.IsDigit).ToArray());
    }

    public static string? Optional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    public static string? State(string? value)
    {
        var normalized = Optional(value)?.ToUpperInvariant();
        return normalized;
    }

    public static bool IsValidState(string? value) =>
        value is not null && States.Contains(value);

    public static bool IsValidCpf(string? value)
    {
        var digits = Digits(value);
        if (digits is null || digits.Length != 11 || digits.Distinct().Count() == 1)
            return false;
        return CheckDigit(digits, 9, 10) == digits[9] - '0'
            && CheckDigit(digits, 10, 11) == digits[10] - '0';
    }

    public static bool IsValidCnpj(string? value)
    {
        var digits = Digits(value);
        if (digits is null || digits.Length != 14 || digits.Distinct().Count() == 1)
            return false;
        return CnpjDigit(digits, 12) == digits[12] - '0'
            && CnpjDigit(digits, 13) == digits[13] - '0';
    }

    private static int CheckDigit(string digits, int length, int weight)
    {
        var sum = 0;
        for (var index = 0; index < length; index++)
            sum += (digits[index] - '0') * (weight - index);
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static int CnpjDigit(string digits, int length)
    {
        var weights = length == 12
            ? new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 }
            : new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var sum = 0;
        for (var index = 0; index < length; index++)
            sum += (digits[index] - '0') * weights[index];
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
