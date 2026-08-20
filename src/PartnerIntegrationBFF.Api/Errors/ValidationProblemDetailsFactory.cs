using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace PartnerIntegrationBFF.Api.Errors;

public static class ValidationProblemDetailsFactory
{
    public static ValidationProblemDetails ToProblemDetails(this ValidationResult result, HttpContext context)
    {
        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.Request.Path,
        };
    }
}
