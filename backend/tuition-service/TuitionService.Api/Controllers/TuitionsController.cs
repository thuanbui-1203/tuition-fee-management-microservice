using Microservices.Common.Api;
using Microsoft.AspNetCore.Mvc;
using TuitionService.Application.Services;

namespace TuitionService.Api.Controllers;

/// <summary>
/// Public tuition lookup API.
/// </summary>
[ApiController]
[Route("api/v1/tuitions")]
public sealed class TuitionsController : ControllerBase
{
    private readonly TuitionApplicationService _service;

    public TuitionsController(TuitionApplicationService service) => _service = service;

    /// <summary>
    /// Looks up a student and their single outstanding (UNPAID) tuition fee by MSSV.
    /// </summary>
    /// <param name="mssv">The student code, e.g. <c>521H0001</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Student and unpaid fee found.</response>
    /// <response code="400">MSSV missing or invalid.</response>
    /// <response code="404">Student not found, or student has no tuition fee.</response>
    /// <response code="409">The student's tuition fee has already been paid.</response>
    [HttpGet]
    public async Task<IActionResult> Lookup([FromQuery] string? mssv, CancellationToken cancellationToken)
    {
        var result = await _service.LookupAsync(mssv, cancellationToken);
        return this.FromResult(result, r => new
        {
            r.Mssv,
            studentName = r.StudentName,
            fee = r.Fee
        });
    }
}
