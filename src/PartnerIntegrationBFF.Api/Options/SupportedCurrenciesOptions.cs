namespace PartnerIntegrationBFF.Api.Options;

public sealed class SupportedCurrenciesOptions
{
    public const string SectionName = "SupportedCurrencies";

    public IReadOnlyCollection<string> Values { get; init; } = Array.Empty<string>();
}
