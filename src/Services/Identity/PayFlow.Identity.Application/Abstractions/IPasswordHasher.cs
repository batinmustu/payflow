namespace PayFlow.Identity.Application.Abstractions;

/// <summary>
/// One-way password hashing. Implementations choose the algorithm (BCrypt,
/// Argon2, etc.); Application layer only knows "hash this" and "verify this".
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plaintext);
    bool Verify(string plaintext, string hash);
}
