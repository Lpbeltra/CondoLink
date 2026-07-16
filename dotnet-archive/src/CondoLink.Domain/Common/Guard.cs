namespace CondoLink.Domain.Common;

public static class Guard
{
    public static T AgainstNull<T>(
        T? value,
        string parameterName)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return value;
    }

    public static string AgainstNullOrWhiteSpace(
        string? value,
        string parameterName,
        int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "O valor não pode ser nulo, vazio ou conter apenas espaços.",
                parameterName);
        }

        var normalizedValue = value.Trim();

        if (maxLength.HasValue && normalizedValue.Length > maxLength.Value)
        {
            throw new ArgumentException(
                $"O valor não pode possuir mais de {maxLength.Value} caracteres.",
                parameterName);
        }

        return normalizedValue;
    }

    public static string? AgainstTooLong(
        string? value,
        string parameterName,
        int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > maxLength)
        {
            throw new ArgumentException(
                $"O valor não pode possuir mais de {maxLength} caracteres.",
                parameterName);
        }

        return normalizedValue;
    }

    public static int AgainstNegative(
        int value,
        string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "O valor não pode ser negativo.");
        }

        return value;
    }

    public static decimal AgainstNegative(
        decimal value,
        string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "O valor não pode ser negativo.");
        }

        return value;
    }

    public static int AgainstZeroOrNegative(
        int value,
        string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "O valor deve ser maior que zero.");
        }

        return value;
    }

    public static decimal AgainstZeroOrNegative(
        decimal value,
        string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "O valor deve ser maior que zero.");
        }

        return value;
    }

    public static T AgainstDefault<T>(
        T value,
        string parameterName)
        where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
        {
            throw new ArgumentException(
                "O valor não pode ser o valor padrão do tipo.",
                parameterName);
        }

        return value;
    }

    public static T AgainstInvalidEnum<T>(
        T value,
        string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "O valor informado não é válido para este enum.");
        }

        return value;
    }

    public static int AgainstOutOfRange(
        int value,
        int minimum,
        int maximum,
        string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"O valor deve estar entre {minimum} e {maximum}.");
        }

        return value;
    }

    public static DateTimeOffset AgainstFutureDate(
        DateTimeOffset value,
        string parameterName)
    {
        if (value > DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "A data não pode estar no futuro.");
        }

        return value;
    }
}