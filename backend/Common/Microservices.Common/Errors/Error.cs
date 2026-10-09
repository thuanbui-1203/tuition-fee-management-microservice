using Microsoft.AspNetCore.Http;

namespace Microservices.Common.Errors;

/// <summary>
/// A domain/application error with a stable <see cref="Code"/>, a human-readable
/// <see cref="Message"/> and the HTTP <see cref="StatusCode"/> it maps to.
/// </summary>
public sealed record Error(string Code, string Message, int StatusCode)
{
    public static Error InvalidInput(string message = "Invalid input.") =>
        new(ErrorCodes.InvalidInput, message, StatusCodes.Status400BadRequest);

    public static Error Unauthenticated(string message = "Authentication is required.") =>
        new(ErrorCodes.Unauthenticated, message, StatusCodes.Status401Unauthorized);

    public static Error Forbidden(string message = "Access denied.") =>
        new(ErrorCodes.Forbidden, message, StatusCodes.Status403Forbidden);

    public static Error NotFound(string message = "Resource not found.") =>
        new(ErrorCodes.NotFound, message, StatusCodes.Status404NotFound);

    public static Error FeeAlreadyPaid(string message = "The tuition fee has already been paid.") =>
        new(ErrorCodes.FeeAlreadyPaid, message, StatusCodes.Status409Conflict);

    public static Error ReleaseConflict(string message = "The tuition fee cannot be released for this transaction.") =>
        new(ErrorCodes.ReleaseConflict, message, StatusCodes.Status409Conflict);

    public static Error OtpAlreadyUsed(string message = "The OTP has already been used.") =>
        new(ErrorCodes.OtpAlreadyUsed, message, StatusCodes.Status409Conflict);

    public static Error OtpIncorrect(string message = "The OTP is incorrect.") =>
        new(ErrorCodes.OtpIncorrect, message, StatusCodes.Status400BadRequest);

    public static Error OtpExpired(string message = "The OTP has expired.") =>
        new(ErrorCodes.OtpExpired, message, StatusCodes.Status410Gone);

    public static Error OtpLocked(string message = "The OTP is locked after too many failed attempts.") =>
        new(ErrorCodes.OtpLocked, message, StatusCodes.Status409Conflict);

    public static Error IdempotencyConflict(string message = "The idempotency key was reused with a different payload.") =>
        new(ErrorCodes.IdempotencyConflict, message, StatusCodes.Status409Conflict);

    public static Error RateLimited(string message = "Too many requests. Please try again later.") =>
        new(ErrorCodes.RateLimited, message, StatusCodes.Status429TooManyRequests);

    public static Error InternalError(string message = "An unexpected error occurred.") =>
        new(ErrorCodes.InternalError, message, StatusCodes.Status500InternalServerError);

    public static Error DependencyDown(string message = "A downstream dependency is unavailable.") =>
        new(ErrorCodes.DependencyDown, message, StatusCodes.Status503ServiceUnavailable);
}
