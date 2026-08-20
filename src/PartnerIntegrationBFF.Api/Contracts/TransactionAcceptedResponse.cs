namespace PartnerIntegrationBFF.Api.Contracts;

public sealed record TransactionAcceptedResponse(
    string TransactionReference,
    string CorrelationId,
    string Status);
