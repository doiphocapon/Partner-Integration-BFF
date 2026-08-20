using RabbitMQ.Client;

namespace PartnerIntegrationBFF.Api.Messaging;

/// <summary>
/// Owns the single long-lived broker connection, shared by the publisher and the health check
/// so neither leaks a connection per call.
/// </summary>
public interface IRabbitMqConnectionProvider
{
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken);
}
