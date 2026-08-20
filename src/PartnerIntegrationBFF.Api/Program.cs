using FluentValidation;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using PartnerIntegrationBFF.Api.Contracts;
using PartnerIntegrationBFF.Api.ErrorHandling;
using PartnerIntegrationBFF.Api.Messaging;
using PartnerIntegrationBFF.Api.Options;
using PartnerIntegrationBFF.Api.Security;
using PartnerIntegrationBFF.Api.Services;
using PartnerIntegrationBFF.Api.Validation;
using Polly;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(ApiKeyAuthenticationOptions.SchemeName, new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthenticationHandler.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Partner API key, e.g. 'X-Api-Key: {your-key}'.",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(ApiKeyAuthenticationOptions.SchemeName, document)] = new List<string>(),
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOptions<ApiKeySecurityOptions>()
    .BindConfiguration(ApiKeySecurityOptions.SectionName)
    .ValidateOnStart();

builder.Services
    .AddAuthentication(ApiKeyAuthenticationOptions.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.SchemeName, _ => { });

builder.Services.AddAuthorization();

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

builder.Services.AddOptions<RabbitMqOptions>()
    .BindConfiguration(RabbitMqOptions.SectionName)
    .ValidateOnStart();

builder.Services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
builder.Services.AddSingleton<ITransactionPublisher, RabbitMqTransactionPublisher>();

builder.Services.AddHealthChecks()
    .AddRabbitMQ(
        sp => sp.GetRequiredService<IRabbitMqConnectionProvider>().GetConnectionAsync(CancellationToken.None),
        name: "rabbitmq");

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
