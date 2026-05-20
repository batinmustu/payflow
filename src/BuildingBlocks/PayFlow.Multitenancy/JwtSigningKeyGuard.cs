using Microsoft.Extensions.Hosting;

namespace PayFlow.Multitenancy;

/// <summary>
/// Stops production deployments from booting with the committed dev signing
/// key. The dev key string is intentionally public (used in
/// <c>appsettings.Development.json</c> across the repo); if it ever signs
/// real tokens the public repo becomes the master key.
/// </summary>
public static class JwtSigningKeyGuard
{
    /// <summary>
    /// The string committed to every <c>appsettings.Development.json</c>.
    /// Treat it as a sentinel — never accept it in production.
    /// </summary>
    public const string DevOnlySigningKey =
        "dev-only-signing-key-replace-in-production-min-32-bytes";

    public static void ThrowIfDevKeyInProduction(string? signingKey, IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);

        if (env.IsProduction() && string.Equals(signingKey, DevOnlySigningKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Identity:Jwt:SigningKey is still the committed dev value. " +
                "Override it via configuration (env var, secret store) before running in production.");
        }
    }
}
