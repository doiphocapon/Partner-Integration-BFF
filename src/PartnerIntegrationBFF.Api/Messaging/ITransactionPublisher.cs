namespace PartnerIntegrationBFF.Api.Messaging;

public interface ITransactionPublisher
{
    Task<PublishResult> PublishAsync(TransactionMessage message, CancellationToken cancellationToken);
}
