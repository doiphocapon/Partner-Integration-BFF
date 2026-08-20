using FluentValidation;
using PartnerIntegrationBFF.Api.Contracts;
using PartnerIntegrationBFF.Api.Options;
using PartnerIntegrationBFF.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOptions<SupportedCurrenciesOptions>()
    .BindConfiguration(SupportedCurrenciesOptions.SectionName)
    .ValidateOnStart();

builder.Services.AddScoped<IValidator<TransactionRequest>, TransactionRequestValidator>();

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
