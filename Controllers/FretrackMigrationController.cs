using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Contracts;
using RepoDbApi.Services;
using System.Net.Http;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/fretrack-migration")]
public class FretrackMigrationController : ControllerBase
{
    private readonly ICargoMigrationService _service;
    private readonly ILogger<FretrackMigrationController> _logger;

    public FretrackMigrationController(
        ICargoMigrationService service,
        ILogger<FretrackMigrationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("single-job")]
    public async Task<IActionResult> MigrateSingleJob([FromBody] CargoMigrationRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.JobNo))
        {
            return BadRequest(CreateFailedResponse(string.Empty, "JobNo is required.", "validation_error"));
        }

        var jobNo = request.JobNo.Trim();

        try
        {
            await TryFetchZohoBillAndInvoiceAsync(jobNo, cancellationToken);

            const int orgId = 18;
            var result = await _service.MigrateSingleJobAsync(jobNo, orgId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Cargo record not found for JobNo: {JobNo}", jobNo);
            return NotFound(CreateFailedResponse(jobNo, ex.Message, "not_found"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Migration setup error for JobNo: {JobNo}", jobNo);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateFailedResponse(jobNo, ex.Message, "setup_error"));
        }
        catch (Exception ex)
        {
            var rootMessage = ex.GetBaseException().Message;
            _logger.LogError(ex, "Failed to migrate cargo for JobNo: {JobNo}", jobNo);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateFailedResponse(jobNo, rootMessage, "migration_failed"));
        }
    }

    private async Task TryFetchZohoBillAndInvoiceAsync(string jobNo, CancellationToken cancellationToken)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var billUrl = $"{baseUrl}/api/vendor-bills/getBillFromZoho?jobNo={Uri.EscapeDataString(jobNo)}";
        var invoiceUrl = $"{baseUrl}/api/vendor-bills/getInvoiceFromZoho?jobNo={Uri.EscapeDataString(jobNo)}";

        using var client = new HttpClient();

        await TryFetchZohoResourceAsync(client, billUrl, "bill", jobNo, cancellationToken);
        await TryFetchZohoResourceAsync(client, invoiceUrl, "invoice", jobNo, cancellationToken);
    }

    private async Task TryFetchZohoResourceAsync(
        HttpClient client,
        string url,
        string resourceLabel,
        string jobNo,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Zoho {ResourceLabel} fetch failed for JobNo {JobNo} with HTTP {StatusCode}: {Content}",
                    resourceLabel,
                    jobNo,
                    (int)response.StatusCode,
                    content);
                return;
            }

            _logger.LogInformation(
                "Zoho {ResourceLabel} fetch completed for JobNo {JobNo}.",
                resourceLabel,
                jobNo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Zoho {ResourceLabel} fetch failed for JobNo {JobNo}. Migration will continue.",
                resourceLabel,
                jobNo);
        }
    }

    [HttpPost("container-only")]
    public async Task<IActionResult> MigrateContainerOnly([FromBody] CargoMigrationRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.JobNo))
        {
            return BadRequest(CreateFailedResponse(string.Empty, "JobNo is required.", "validation_error"));
        }

        var jobNo = request.JobNo.Trim();

        try
        {
            var result = await _service.MigrateContainerOnlyAsync(jobNo, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Cargo record not found for JobNo: {JobNo}", jobNo);
            return NotFound(CreateFailedResponse(jobNo, ex.Message, "not_found"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Migration setup error for JobNo: {JobNo}", jobNo);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateFailedResponse(jobNo, ex.Message, "setup_error"));
        }
        catch (Exception ex)
        {
            var rootMessage = ex.GetBaseException().Message;
            _logger.LogError(ex, "Failed to stage container only for JobNo: {JobNo}", jobNo);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateFailedResponse(jobNo, rootMessage, "migration_failed"));
        }
    }

    private static CargoMigrationResponse CreateFailedResponse(string jobNo, string message, string status)
    {
        return new CargoMigrationResponse
        {
            JobNo = jobNo,
            Status = status,
            Message = message,
            ActualMigrationCompleted = false
        };
    }
}
