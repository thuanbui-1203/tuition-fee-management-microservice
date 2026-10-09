using Microservices.Common.Api;
using Microsoft.AspNetCore.Mvc;
using OtpService.Application.Services;

namespace OtpService.Api.Controllers;

/// <summary>
/// Internal OTP APIs used by payment-service. All endpoints require service-to-service
/// authentication and are never exposed through the public gateway.
/// </summary>
[ApiController]
[Route("internal/otp")]
public sealed class InternalOtpController : ControllerBase
{
    private readonly OtpApplicationService _service;

    public InternalOtpController(OtpApplicationService service) => _service = service;

    /// <summary>
    /// Issues a new OTP for a transaction and enqueues the OTP email (via the outbox).
    /// Idempotent when the <c>Idempotency-Key</c> header is reused.
    /// </summary>
    /// <response code="201">OTP created and email enqueued.</response>
    /// <response code="200">Idempotent replay — the original OTP is returned.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="409">Idempotency key reused with a different transaction.</response>
    /// <response code="429">Rate limited.</response>
    [HttpPost("issue")]
    public async Task<IActionResult> Issue([FromBody] IssueOtpRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return this.ErrorResponse(Microservices.Common.Errors.Error.InvalidInput("Request body is required."));
        }

        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var result = await _service.IssueAsync(
            request.TransactionId,
            request.Email,
            idempotencyKey,
            HttpContext.TraceIdentifier,
            cancellationToken);

        var statusCode = result.IsSuccess && result.Value.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created;
        return this.FromResult(result, r => new { otpId = r.OtpId }, statusCode);
    }

    /// <summary>Verifies an OTP for a transaction (one-time use, attempt-limited).</summary>
    /// <response code="200">OTP is valid and has been atomically marked used.</response>
    /// <response code="400">Invalid input or incorrect OTP.</response>
    /// <response code="404">No usable OTP for this transaction.</response>
    /// <response code="409">OTP already used or locked.</response>
    /// <response code="410">OTP expired.</response>
    /// <response code="429">Rate limited.</response>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return this.ErrorResponse(Microservices.Common.Errors.Error.InvalidInput("Request body is required."));
        }

        var result = await _service.VerifyAsync(request.TransactionId, request.Otp, cancellationToken);
        return this.FromResult(result, r => new { valid = r.Valid });
    }
}

/// <summary>OTP issuance request.</summary>
public sealed record IssueOtpRequest(Guid TransactionId, string Email);

/// <summary>OTP verification request.</summary>
public sealed record VerifyOtpRequest(Guid TransactionId, string Otp);
