using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using PartnerIntegrationBFF.Api.Messaging;
using PartnerIntegrationBFF.Api.Services;

namespace PartnerIntegrationBFF.IntegrationTests.TestSupport;

/// <summary>
/// Boots the real ASP.NET Core pipeline (routing, auth, validation, controller) but substitutes
/// the two external dependencies (partner verification, message broker) so tests are
/// deterministic and don't need a live RabbitMQ or rely on the dummy endpoint's real randomness.
/// The verification/retry pipeline itself is covered separately by
/// HttpPartnerVerificationServiceTests in the unit test project.
/// </summary>
public sealed class TransactionsApiFactory : WebApplicationFactory<Program>
{
    public IPartnerVerificationService VerificationService { get; } = Substitute.For<IPartnerVerificationService>();

    public ITransactionPublisher Publisher { get; } = Substitute.For<ITransactionPublisher>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPartnerVerificationService>();
            services.AddSingleton(VerificationService);

            services.RemoveAll<ITransactionPublisher>();
            services.AddSingleton(Publisher);
        });
    }
}
