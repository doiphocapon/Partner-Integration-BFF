using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerIntegrationBFF.Api.Contracts;
using PartnerIntegrationBFF.Api.Errors;
using PartnerIntegrationBFF.Api.Messaging;
using PartnerIntegrationBFF.Api.Security;
using PartnerIntegrationBFF.Api.Services;

namespace PartnerIntegrationBFF.Api.Controllers;

[ApiController]
[Route("api/v1/partner/transactions")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
public sealed class PartnerTransactionsController : ControllerBase
{
    private readonly IValidator<TransactionRequest> _validator;
    private readonly IPartnerVerificationService _verificationService;
    private readonly ITransactionPublisher _publisher;
    private readonly ILogger<PartnerTransactionsController> _logger;

    public PartnerTransactionsController(
        IValidator<TransactionRequest> validator,
        IPartnerVerificationService verificationService,
        ITransactionPublisher publisher,
        ILogger<PartnerTransactionsController> logger)
    {
        _validator = validator;
        _verificationService = verificationService;
        _publisher = publisher;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(TransactionAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Post([FromBody] TransactionRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.ToProblemDetails(HttpContext));
        }

        var verification = await _verificationService.VerifyAsync(request.PartnerId!, cancellationToken);

        if (verification.Status == PartnerVerificationStatus.NotVerified)
        {
            _logger.LogInformation(
                "Partner {PartnerId} could not be verified for transaction {TransactionReference}.",
                request.PartnerId,
                request.TransactionReference);

            return UnprocessableEntity(ProblemFor(
                StatusCodes.Status422UnprocessableEntity,
                "Partner not verified.",
                $"Partner '{request.PartnerId}' could not be verified."));
        }

        if (verification.Status == PartnerVerificationStatus.ServiceUnavailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ProblemFor(
                StatusCodes.Status503ServiceUnavailable,
                "Partner verification unavailable.",
                "The partner verification service is temporarily unavailable. Please retry."));
        }

        var correlationId = Guid.NewGuid().ToString();
        var message = new TransactionMessage(
            correlationId,
            request.PartnerId!,
            request.TransactionReference!,
            request.Amount,
            request.Currency!,
            request.Timestamp!.Value);

        var publishResult = await _publisher.PublishAsync(message, cancellationToken);
        if (publishResult.Status != PublishStatus.Published)
        {
            _logger.LogError(
                "Failed to publish transaction {TransactionReference} (correlation {CorrelationId}): {Error}",
                request.TransactionReference,
                correlationId,
                publishResult.Error);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, ProblemFor(
                StatusCodes.Status503ServiceUnavailable,
                "Unable to queue transaction.",
                "The transaction could not be queued for processing. Please retry."));
        }

        return Accepted(new TransactionAcceptedResponse(request.TransactionReference!, correlationId, "Accepted"));
    }

    private ProblemDetails ProblemFor(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
    };
}
