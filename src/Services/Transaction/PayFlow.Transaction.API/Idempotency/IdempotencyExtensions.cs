using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace PayFlow.Transaction.API.Idempotency;

internal static class IdempotencyExtensions
{
    public static IServiceCollection AddPayFlowIdempotency(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<IdempotencyOptions>(configuration.GetSection(IdempotencyOptions.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<IdempotencyOptions>>().Value;
            return ConnectionMultiplexer.Connect(options.RedisConnectionString);
        });

        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        return services;
    }

    public static IApplicationBuilder UsePayFlowIdempotency(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<IdempotencyMiddleware>();
    }
}
