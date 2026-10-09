using Microservices.Common.Api;
using Microsoft.AspNetCore.Mvc;
using TuitionService.Application.Services;

namespace TuitionService.Api.Controllers;

/// <summary>
/// Internal (service-to-service) tuition APIs used by payment-service during the payment
/// saga. These endpoints are protected by the internal API-key middleware and are never
/// exposed through the public gateway.
/// </summary>
[ApiController]
[Route("internal/tuitions")]
public sealed class InternalTuitionsController : ControllerBase
{
    private readonly TuitionApplicationService _service;

    public InternalTuitionsController(TuitionApplicationService service) => _service = service;

    /// <summary>Atomically claims an unpaid tuition fee for a transaction.</summary>
    /// <response code="200">Fee claimed (or already claimed by this transaction — idempotent).</response>
    /// <response code="404">Fee not found.</response>
    /// <response code="409">Fee already paid by another transaction.</response>
    [HttpPost("{feeId:long}/claim")]
    public async Task<IActionResult> Claim(long feeId, [FromBody] ClaimFeeRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return this.ErrorResponse(Microservices.Common.Errors.Error.InvalidInput("Request body is required."));
        }

        if (request.TransactionId == Guid.Empty)
        {
            return this.ErrorResponse(Microservices.Common.Errors.Error.InvalidInput("transactionId is required."));
        }

        var result = await _service.ClaimAsync(feeId, request.TransactionId, cancellationToken);
        return this.FromResult(result, () => new { success = true });
    }

    /// <summary>
    /// Releases a claimed fee during saga compensation. Only the transaction that currently
    /// holds the fee may release it.
    /// </summary>
    /// <response code="200">Fee released (or already unpaid — idempotent).</response>
    /// <response code="404">Fee not found.</response>
    /// <response code="409">Fee is held by a different transaction.</response>
    [HttpPost("{feeId:long}/release")]
    public async Task<IActionResult> Release(long feeId, [FromBody] ReleaseFeeRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return this.ErrorResponse(Microservices.Common.Errors.Error.InvalidInput("Request body is required."));
        }

        if (request.TransactionId == Guid.Empty)
        {
            return this.ErrorResponse(Microservices.Common.Errors.Error.InvalidInput("transactionId is required."));
        }

        var result = await _service.ReleaseAsync(feeId, request.TransactionId, cancellationToken);
        return this.FromResult(result, () => new { success = true });
    }
}

/// <summary>Claim request body.</summary>
public sealed record ClaimFeeRequest(Guid TransactionId);

/// <summary>Release request body.</summary>
public sealed record ReleaseFeeRequest(Guid TransactionId);
