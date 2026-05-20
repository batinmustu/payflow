using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PayFlow.Transaction.Application.Abstractions;
using PayFlow.Transaction.Infrastructure.Payments;
using PayFlow.Transaction.Infrastructure.Persistence;
using PayFlow.Transaction.Infrastructure.Routing;

namespace PayFlow.Transaction.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowTransactionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("TransactionDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:TransactionDb is not configured for the Transaction service.");

        services.AddDbContext<TransactionDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    tableName: "__ef_migrations_history",
                    schema: TransactionDbContext.SchemaName)));

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.Configure<RoutingOptions>(configuration.GetSection(RoutingOptions.SectionName));
        services.AddSingleton<IRoutingPolicy, StaticRoutingPolicy>();

        services.Configure<PaymentServiceOptions>(configuration.GetSection(PaymentServiceOptions.SectionName));
        services.AddHttpClient<IPaymentClient, HttpPaymentClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<PaymentServiceOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        return services;
    }
}
