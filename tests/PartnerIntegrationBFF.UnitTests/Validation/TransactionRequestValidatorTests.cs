using FluentAssertions;
using Microsoft.Extensions.Options;
using PartnerIntegrationBFF.Api.Contracts;
using PartnerIntegrationBFF.Api.Options;
using PartnerIntegrationBFF.Api.Validation;

namespace PartnerIntegrationBFF.UnitTests.Validation;

public class TransactionRequestValidatorTests
{
    private readonly TransactionRequestValidator _sut = new(
        Options.Create(new SupportedCurrenciesOptions { Values = new[] { "USD", "EUR" } }));

    private static TransactionRequest ValidRequest() => new()
    {
        PartnerId = "P-1001",
        TransactionReference = "TXN-99823",
        Amount = 250.00m,
        Currency = "USD",
        Timestamp = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void Validate_FullyValidRequest_HasNoErrors()
    {
        var result = _sut.Validate(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_MissingPartnerId_HasError(string? partnerId)
    {
        var request = ValidRequest() with { PartnerId = partnerId };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TransactionRequest.PartnerId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingTransactionReference_HasError(string? reference)
    {
        var request = ValidRequest() with { TransactionReference = reference };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TransactionRequest.TransactionReference));
    }

    [Fact]
    public void Validate_MissingTimestamp_HasError()
    {
        var request = ValidRequest() with { Timestamp = null };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TransactionRequest.Timestamp));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-250.5)]
    public void Validate_AmountNotGreaterThanZero_HasError(decimal amount)
    {
        var request = ValidRequest() with { Amount = amount };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TransactionRequest.Amount));
    }

    [Fact]
    public void Validate_PositiveAmount_HasNoAmountError()
    {
        var request = ValidRequest() with { Amount = 0.01m };

        var result = _sut.Validate(request);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(TransactionRequest.Amount));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingCurrency_HasError(string? currency)
    {
        var request = ValidRequest() with { Currency = currency };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TransactionRequest.Currency));
    }

    [Fact]
    public void Validate_UnsupportedCurrency_HasError()
    {
        var request = ValidRequest() with { Currency = "XXX" };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TransactionRequest.Currency));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("eur")]
    public void Validate_SupportedCurrency_IsCaseInsensitiveAndValid(string currency)
    {
        var request = ValidRequest() with { Currency = currency };

        var result = _sut.Validate(request);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(TransactionRequest.Currency));
    }
}
