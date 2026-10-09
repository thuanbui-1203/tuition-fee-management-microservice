using Microservices.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Microservices.Common.Api;

/// <summary>ASP.NET Core service registration helpers for a unified error contract.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers controllers with a custom model-state response factory so automatic 400
    /// validation failures use the unified error body instead of ProblemDetails.
    /// </summary>
    public static IMvcBuilder AddControllersWithUnifiedValidation(this IServiceCollection services)
    {
        return services.AddControllers().ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var message = context.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Invalid input.";

                var body = new ErrorResponse(
                    ErrorCodes.InvalidInput,
                    message,
                    DateTimeOffset.UtcNow,
                    context.HttpContext.TraceIdentifier);

                return new ObjectResult(body) { StatusCode = StatusCodes.Status400BadRequest };
            };
        });
    }
}
