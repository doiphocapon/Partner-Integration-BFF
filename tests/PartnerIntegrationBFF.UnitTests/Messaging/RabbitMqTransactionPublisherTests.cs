using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PartnerIntegrationBFF.Api.Messaging;
using PartnerIntegrationBFF.Api.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace PartnerIntegrationBFF.UnitTests.Messaging;

public class RabbitMqTransactionPublisherTests
{
    private readonly IRabbitMqConnectionProvider _connectionProvider = Substitute.For<IRabbitMqConnectionProvider>();
    private readonly IConnection _connection = Substitute.For<IConnection>();
    private readonly IChannel _channel = Substitute.For<IChannel>();

    private static readonly TransactionMessage Message = new(
        CorrelationId: "corr-1",
        PartnerId: "P-1001",
        TransactionReference: "TXN-99823",
        Amount: 250.00m,
        Currency: "USD",
        Timestamp: DateTimeOffset.UtcNow);

    public RabbitMqTransactionPublisherTests()
    {
        _connectionProvider.GetConnectionAsync(Arg.Any<CancellationToken>()).Returns(_connection);
        _connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>())
            .Returns(_channel);
    }

    [Fact]
    public async Task PublishAsync_BrokerAcceptsMessage_ReturnsPublished()
    {
        var sut = CreateSut();

        var result = await sut.PublishAsync(Message, CancellationToken.None);

        result.Status.Should().Be(PublishStatus.Published);
        await _channel.Received(1).BasicPublishAsync(
            Arg.Is<string>(exchange => exchange == string.Empty),
            Arg.Is<string>(routingKey => routingKey == "partner-transactions"),
            Arg.Is<bool>(mandatory => mandatory),
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_BrokerRejectsOrConnectionFails_ReturnsFailedWithoutThrowing()
    {
        _channel.BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker did not confirm the publish"));

        var sut = CreateSut();

        var result = await sut.PublishAsync(Message, CancellationToken.None);

        result.Status.Should().Be(PublishStatus.Failed);
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PublishAsync_ConnectionUnavailable_ReturnsFailedWithoutThrowing()
    {
        _connectionProvider.GetConnectionAsync(Arg.Any<CancellationToken>())
            .Throws(new BrokerUnreachableException(new Exception("connection refused")));

        var sut = CreateSut();

        var result = await sut.PublishAsync(Message, CancellationToken.None);

        result.Status.Should().Be(PublishStatus.Failed);
    }

    private RabbitMqTransactionPublisher CreateSut()
    {
        var options = Options.Create(new RabbitMqOptions
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest",
            QueueName = "partner-transactions",
        });

        return new RabbitMqTransactionPublisher(
            _connectionProvider,
            options,
            NullLogger<RabbitMqTransactionPublisher>.Instance);
    }
}
