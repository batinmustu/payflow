using PayFlow.Identity.Application.Abstractions;

namespace PayFlow.Identity.Infrastructure.Security;

internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    // Work factor 12 is the modern starting point — slow enough to deter
    // offline brute force, fast enough to keep registration UX usable.
    private const int WorkFactor = 12;

    public string Hash(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return BCrypt.Net.BCrypt.HashPassword(plaintext, workFactor: WorkFactor);
    }

    public bool Verify(string plaintext, string hash)
    {
        if (string.IsNullOrWhiteSpace(plaintext) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(plaintext, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Malformed hash on a stored row — treat as a failed verification
            // rather than crashing the request.
            return false;
        }
    }
}
