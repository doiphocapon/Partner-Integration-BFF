using System.Text.Json;
using Microsoft.Extensions.Options;
using PartnerIntegrationBFF.Api.Options;
using RabbitMQ.Client;

namespace PartnerIntegrationBFF.Api.Messaging;

public sealed class RabbitMqTransactionPublisher : ITransactionPublisher
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqTransactionPublisher> _logger;

    public RabbitMqTransactionPublisher(
        IRabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqTransactionPublisher> logger)
    {
        _connectionProvider = connectionProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PublishResult> PublishAsync(TransactionMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            await using var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                cancellationToken);

            await channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                noWait: false,
                cancellationToken: cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                Persistent = true,
                MessageId = message.CorrelationId,
            };

            // With publisher confirmations enabled on the channel, this awaits the broker's
            // ack/nack before completing — it throws if the broker didn't accept the message,
            // so we never report success for a message that never reached the queue.
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _options.QueueName,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            return PublishResult.Published();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Broad by design: RabbitMQ.Client surfaces broker/network/confirmation failures as
            // several distinct exception types. Any of them means "not published" — the caller
            // must get a typed failure, never a false-positive success or an unhandled 500.
            _logger.LogError(
                ex,
                "Failed to publish transaction {TransactionReference} (correlation {CorrelationId}) to RabbitMQ.",
                message.TransactionReference,
                message.CorrelationId);
            return PublishResult.Failed(ex.Message);
        }
    }
}
