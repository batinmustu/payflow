using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace PayFlow.Notification.Infrastructure.Messaging;

/// <summary>
/// Lazy singleton <see cref="IConnection"/> for the Notification service.
/// One TCP connection per process, channels created per use. Topology is
/// declared once on first connect via <see cref="RabbitMqTopology"/>.
///
/// We connect lazily (first call to <see cref="GetConnection"/>) so the API
/// doesn't fail to start if the broker is briefly unavailable — the first
/// publish/consume just blocks until it's reachable.
/// </summary>
internal sealed class RabbitMqConnectionProvider : IDisposable
{
    private readonly object _gate = new();
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConnectionProvider> _logger;
    private IConnection? _connection;
    private bool _topologyDeclared;

    public RabbitMqConnectionProvider(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqConnectionProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
    }

    public IConnection GetConnection()
    {
        if (_connection is { IsOpen: true }) return _connection;

        lock (_gate)
        {
            if (_connection is { IsOpen: true }) return _connection;

            if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            {
                throw new InvalidOperationException(
                    "RabbitMQ:ConnectionString is not configured. " +
                    "Either configure it or remove the RabbitMQ-backed retry queue from DI.");
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(_options.ConnectionString),
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                ClientProvidedName = "payflow-notification",
            };

            _connection = factory.CreateConnection();
            _logger.LogInformation(
                "RabbitMQ connection established to {Endpoint}.", _connection.Endpoint);

            if (!_topologyDeclared)
            {
                RabbitMqTopology.Declare(_connection, _options);
                _topologyDeclared = true;
            }
            return _connection;
        }
    }

    public void Dispose()
    {
        try { _connection?.Close(); } catch { /* shutdown best-effort */ }
        _connection?.Dispose();
    }
}
