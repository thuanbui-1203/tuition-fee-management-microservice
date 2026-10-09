using Microservices.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Microservices.Common.Api;

/// <summary>
/// Converts <see cref="Result"/> and <see cref="Result{T}"/> into consistent ASP.NET Core
/// action results, producing the unified error body on failure.
/// </summary>
public static class ControllerResultExtensions
{
    public static IActionResult FromResult<T>(
        this ControllerBase controller,
        Result<T> result,
        Func<T, object> map,
        int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(map(result.Value)) { StatusCode = successStatusCode };
        }

        return controller.ErrorResponse(result.Error);
    }

    public static IActionResult FromResult(this ControllerBase controller, Result result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return new StatusCodeResult(successStatusCode);
        }

        return controller.ErrorResponse(result.Error);
    }

    public static IActionResult FromResult(this ControllerBase controller, Result result, Func<object> successBody, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(successBody()) { StatusCode = successStatusCode };
        }

        return controller.ErrorResponse(result.Error);
    }

    public static IActionResult ErrorResponse(this ControllerBase controller, Error error)
    {
        var body = new ErrorResponse(error.Code, error.Message, DateTimeOffset.UtcNow, controller.HttpContext.TraceIdentifier);
        return new ObjectResult(body) { StatusCode = error.StatusCode };
    }
}
