namespace CondoLink.Domain;

public static class BrazilianPhoneNumber
{
    public static string? Normalize(string? value) =>
        PhoneNumberNormalizer.Normalize(value);

    public static bool IsValidOptional(string? value) =>
        PhoneNumberNormalizer.IsValidOptional(value);
}
