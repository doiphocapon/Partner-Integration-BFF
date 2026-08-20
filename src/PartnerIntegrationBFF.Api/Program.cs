using FluentValidation;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using PartnerIntegrationBFF.Api.Contracts;
using PartnerIntegrationBFF.Api.Options;
using PartnerIntegrationBFF.Api.Services;
using PartnerIntegrationBFF.Api.Validation;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOptions<SupportedCurrenciesOptions>()
    .BindConfiguration(SupportedCurrenciesOptions.SectionName)
    .ValidateOnStart();

builder.Services.AddOptions<PartnerVerificationOptions>()
    .BindConfiguration(PartnerVerificationOptions.SectionName)
    .ValidateOnStart();

builder.Services.AddScoped<IValidator<TransactionRequest>, TransactionRequestValidator>();

builder.Services
    .AddHttpClient<IPartnerVerificationService, HttpPartnerVerificationService>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<PartnerVerificationOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    })
    .AddResilienceHandler("partner-verification", (pipeline, context) =>
    {
        var options = context.ServiceProvider.GetRequiredService<IOptions<PartnerVerificationOptions>>().Value;

        // Retries only transient failures (5xx/408/HttpRequestException/timeout) by default;
        // no circuit breaker — a single-request exercise doesn't need sustained-outage state.
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetryAttempts,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(options.RetryBaseDelayMilliseconds),
        });
        pipeline.AddTimeout(TimeSpan.FromMilliseconds(options.AttemptTimeoutMilliseconds));
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
