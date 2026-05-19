using System.Text.RegularExpressions;
using PayFlow.SharedKernel;

namespace PayFlow.Identity.Domain.Tenants;

/// <summary>
/// A logically isolated customer of PayFlow. Every business row in every
/// service ultimately carries this id (see
/// docs/database/multi-tenancy-isolation.md). The Tenant aggregate itself
/// owns only the tenant's lifecycle and identity — not the data it scopes.
/// </summary>
public sealed partial class Tenant : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public TenantStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Constructor is internal-style: only invoked by Create and by EF Core
    // through constructor binding once we wire infrastructure.
    private Tenant(
        Guid id,
        string name,
        string slug,
        TenantStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Status = status;
        CreatedAt = createdAt;
    }

    public static Result<Tenant> Create(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Tenant>("TENANT_NAME_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<Tenant>("TENANT_SLUG_REQUIRED");
        }

        var normalisedSlug = slug.Trim().ToLowerInvariant();
        if (!SlugRegex().IsMatch(normalisedSlug))
        {
            return Result.Failure<Tenant>("TENANT_SLUG_INVALID");
        }

        var tenant = new Tenant(
            id: Guid.NewGuid(),
            name: name.Trim(),
            slug: normalisedSlug,
            status: TenantStatus.Active,
            createdAt: DateTimeOffset.UtcNow);

        tenant.Raise(new TenantCreatedDomainEvent(tenant.Id, tenant.Name, tenant.Slug));
        return Result.Success(tenant);
    }

    public void Deactivate()
    {
        if (Status == TenantStatus.Inactive)
        {
            return;
        }

        Status = TenantStatus.Inactive;
    }

    // Lowercase alphanumeric with internal dashes, 3–50 chars, no leading or
    // trailing dash. Matches what a URL slug needs to look like for a tenant
    // (e.g. https://payflow.example/{slug}).
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
