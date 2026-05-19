using PayFlow.SharedKernel;

namespace PayFlow.Identity.Domain.Users;

/// <summary>
/// Normalised email value object. Construction goes through <see cref="Create"/>;
/// the constructor stays private so callers always see validation errors.
/// </summary>
public sealed record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Failure<Email>("EMAIL_REQUIRED");
        }

        var trimmed = raw.Trim().ToLowerInvariant();

        if (trimmed.Length > 254 || !IsShapedLikeEmail(trimmed))
        {
            return Result.Failure<Email>("EMAIL_INVALID");
        }

        return Result.Success(new Email(trimmed));
    }

    public override string ToString() => Value;

    private static bool IsShapedLikeEmail(string value)
    {
        // Intentionally lightweight: an `@`, something on each side, a `.` in the
        // domain portion. Full RFC 5322 belongs to a library — we keep the domain
        // model honest and let the infrastructure layer enforce stricter rules
        // (e.g. a mail-delivery sanity check) where it matters.
        var atIndex = value.IndexOf('@');
        if (atIndex <= 0 || atIndex >= value.Length - 1)
        {
            return false;
        }

        var domain = value[(atIndex + 1)..];
        if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.'))
        {
            return false;
        }

        return true;
    }
}
