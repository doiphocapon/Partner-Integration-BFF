namespace PartnerIntegrationBFF.Api.Options;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public required string HostName { get; init; }

    public int Port { get; init; } = 5672;

    public required string UserName { get; init; }

    public required string Password { get; init; }

    public string VirtualHost { get; init; } = "/";

    public string QueueName { get; init; } = "partner-transactions";
}
