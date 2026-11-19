using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Contracts;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/fretrack-migration")]
public sealed class SalesQuoteSyncController(
    ISalesQuoteSyncService service, ILogger<SalesQuoteSyncController> logger) : ControllerBase
{
    [HttpPost("single-salesquote")]
    [ProducesResponseType(typeof(SalesQuoteChargesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SalesQuoteSyncFailure), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Sync([FromBody] SalesQuoteSyncRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.SyncAsync(request, cancellationToken);
            return request.ChargesOnly ? Ok(SalesQuoteChargesResponse.From(result)) : Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (SalesQuoteSyncConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var failure = SalesQuoteSyncDiagnostics.Describe(ex, HttpContext.TraceIdentifier);
            logger.LogError(ex, "Sales quote sync failed. TraceId={TraceId}; Stage={Stage}; SqlErrorNumber={SqlErrorNumber}",
                failure.TraceId, failure.Stage, failure.SqlErrorNumber);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, failure);
        }
    }
}
