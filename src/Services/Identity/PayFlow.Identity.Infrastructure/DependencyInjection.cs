using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Identity.Application.Abstractions;
using PayFlow.Identity.Infrastructure.Persistence;
using PayFlow.Identity.Infrastructure.Security;

namespace PayFlow.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:IdentityDb is not configured for the Identity service.");

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    tableName: "__ef_migrations_history",
                    schema: IdentityDbContext.SchemaName)));

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        return services;
    }
}
