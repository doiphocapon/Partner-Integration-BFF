using FluentValidation;
using Microsoft.Extensions.Options;
using PartnerIntegrationBFF.Api.Contracts;
using PartnerIntegrationBFF.Api.Options;

namespace PartnerIntegrationBFF.Api.Validation;

public sealed class TransactionRequestValidator : AbstractValidator<TransactionRequest>
{
    public TransactionRequestValidator(IOptions<SupportedCurrenciesOptions> currencyOptions)
    {
        var supportedCurrencies = new HashSet<string>(
            currencyOptions.Value.Values,
            StringComparer.OrdinalIgnoreCase);

        RuleFor(x => x.PartnerId)
            .NotEmpty().WithMessage("partnerId is required.");

        RuleFor(x => x.TransactionReference)
            .NotEmpty().WithMessage("transactionReference is required.");

        RuleFor(x => x.Timestamp)
            .NotNull().WithMessage("timestamp is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("amount must be greater than zero.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("currency is required.");

        RuleFor(x => x.Currency)
            .Must(currency => currency is not null && supportedCurrencies.Contains(currency))
            .WithMessage(x => $"currency '{x.Currency}' is not supported.")
            .When(x => !string.IsNullOrEmpty(x.Currency));
    }
}
