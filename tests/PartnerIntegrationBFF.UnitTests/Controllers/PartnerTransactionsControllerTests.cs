using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PartnerIntegrationBFF.Api.Contracts;
using PartnerIntegrationBFF.Api.Controllers;
using PartnerIntegrationBFF.Api.Messaging;
using PartnerIntegrationBFF.Api.Services;

namespace PartnerIntegrationBFF.UnitTests.Controllers;

public class PartnerTransactionsControllerTests
{
    private readonly IValidator<TransactionRequest> _validator = Substitute.For<IValidator<TransactionRequest>>();
    private readonly IPartnerVerificationService _verificationService = Substitute.For<IPartnerVerificationService>();
    private readonly ITransactionPublisher _publisher = Substitute.For<ITransactionPublisher>();

    private static readonly TransactionRequest ValidRequest = new()
    {
        PartnerId = "P-1001",
        TransactionReference = "TXN-99823",
        Amount = 250.00m,
        Currency = "USD",
        Timestamp = DateTimeOffset.UtcNow,
    };

    public PartnerTransactionsControllerTests()
    {
        _validator.ValidateAsync(Arg.Any<TransactionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
    }

    [Fact]
    public async Task Post_InvalidRequest_Returns400AndNeverCallsVerificationOrPublisher()
    {
        _validator.ValidateAsync(Arg.Any<TransactionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Amount", "amount must be greater than zero.") }));

        var sut = CreateController();

        var result = await sut.Post(ValidRequest with { Amount = 0 }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        await _verificationService.DidNotReceive().VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<TransactionMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_PartnerNotVerified_Returns422AndNeverPublishes()
    {
        _verificationService.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PartnerVerificationResult.NotVerified());

        var sut = CreateController();

        var result = await sut.Post(ValidRequest, CancellationToken.None);

        result.Should().BeOfType<UnprocessableEntityObjectResult>();
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<TransactionMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_VerificationServiceUnavailable_Returns503AndNeverPublishes()
    {
        _verificationService.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PartnerVerificationResult.Unavailable());

        var sut = CreateController();

        var result = await sut.Post(ValidRequest, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<TransactionMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_VerifiedButPublishFails_Returns503()
    {
        _verificationService.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PartnerVerificationResult.Verified());
        _publisher.PublishAsync(Arg.Any<TransactionMessage>(), Arg.Any<CancellationToken>())
            .Returns(PublishResult.Failed("broker unavailable"));

        var sut = CreateController();

        var result = await sut.Post(ValidRequest, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Post_ValidAndVerified_PublishesExactlyOnceAndReturns202()
    {
        _verificationService.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PartnerVerificationResult.Verified());
        _publisher.PublishAsync(Arg.Any<TransactionMessage>(), Arg.Any<CancellationToken>())
            .Returns(PublishResult.Published());

        var sut = CreateController();

        var result = await sut.Post(ValidRequest, CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        var body = accepted.Value.Should().BeOfType<TransactionAcceptedResponse>().Subject;
        body.TransactionReference.Should().Be(ValidRequest.TransactionReference);
        body.Status.Should().Be("Accepted");

        await _publisher.Received(1).PublishAsync(
            Arg.Is<TransactionMessage>(m =>
                m.PartnerId == ValidRequest.PartnerId &&
                m.TransactionReference == ValidRequest.TransactionReference &&
                m.Amount == ValidRequest.Amount &&
                m.Currency == ValidRequest.Currency),
            Arg.Any<CancellationToken>());
    }

    private PartnerTransactionsController CreateController() => new(
        _validator,
        _verificationService,
        _publisher,
        NullLogger<PartnerTransactionsController>.Instance)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        },
    };
}
