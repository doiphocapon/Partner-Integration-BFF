namespace PartnerIntegrationBFF.Api.Messaging;

public sealed record TransactionMessage(
    string CorrelationId,
    string PartnerId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateTimeOffset Timestamp);
