namespace CondoLink.Api.Common;

public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundAppException(string message) : AppException(message);

public sealed class ForbiddenAppException(string message) : AppException(message);

public sealed class ConflictAppException(string message) : AppException(message);

public sealed class UnauthorizedAppException(string message) : AppException(message);

public sealed class ValidationAppException(
    string message,
    IReadOnlyDictionary<string, string[]>? errors = null)
    : AppException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } =
        errors ?? new Dictionary<string, string[]>();
}
