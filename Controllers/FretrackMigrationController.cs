using System.Threading.Channels;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Contracts;
using RepoDbApi.Services;
using System.Diagnostics;
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

    // Same migration and validation as single-job; stream only observed milestones.
    [HttpPost("single-job/progress")]
    public async Task MigrateSingleJobWithProgress([FromBody] CargoMigrationRequest request, CancellationToken cancellationToken)
    {
        Response.ContentType = "application/x-ndjson";
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        var events = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        var producer = Task.Run(async () =>
        {
            MigrationProgress.Current.Value = events.Writer;
            try
            {
                var result = await MigrateSingleJob(request, cancellationToken);
                if (result is ObjectResult response && (response.StatusCode ?? 200) < 400)
                    events.Writer.TryWrite(new { kind = "result", result = response.Value });
                else
                    events.Writer.TryWrite(new { kind = "failure", status = (result as ObjectResult)?.StatusCode ?? 500 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Streaming migration failed");
                events.Writer.TryWrite(new { kind = "failure", status = 500 });
            }
            finally { MigrationProgress.Current.Value = null; events.Writer.TryComplete(); }
        }, CancellationToken.None);
        try
        {
            await foreach (var item in events.Reader.ReadAllAsync(cancellationToken))
            {
                await Response.WriteAsync(JsonSerializer.Serialize(item, new JsonSerializerOptions(JsonSerializerDefaults.Web)) + "\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        finally { await producer; }
    }

    [HttpPost("single-job")]
    public async Task<IActionResult> MigrateSingleJob([FromBody] CargoMigrationRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.JobNo))
        {
            return BadRequest(CreateFailedResponse(string.Empty, "JobNo is required.", "validation_error"));
        }

        var jobNo = request.JobNo.Trim();
        var migrationStopwatch = Stopwatch.StartNew();

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
        finally
        {
            _logger.LogInformation(
                "Single-job migration API completed for JobNo {JobNo} in {ElapsedMilliseconds} ms.",
                jobNo,
                migrationStopwatch.ElapsedMilliseconds);
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
        MigrationProgress.Report("zoho-" + resourceLabel, "running");
        var resourceStopwatch = Stopwatch.StartNew();
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                MigrationProgress.Report("zoho-" + resourceLabel, "warning");
                _logger.LogWarning(
                    "Zoho {ResourceLabel} fetch failed for JobNo {JobNo} after {ElapsedMilliseconds} ms with HTTP {StatusCode}: {Content}",
                    resourceLabel,
                    jobNo,
                    resourceStopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode,
                    content);
                return;
            }

            MigrationProgress.Report("zoho-" + resourceLabel, "completed");
            _logger.LogInformation(
                "Zoho {ResourceLabel} fetch completed for JobNo {JobNo} in {ElapsedMilliseconds} ms.",
                resourceLabel,
                jobNo,
                resourceStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            MigrationProgress.Report("zoho-" + resourceLabel, "warning");
            _logger.LogWarning(
                ex,
                "Zoho {ResourceLabel} fetch failed for JobNo {JobNo} after {ElapsedMilliseconds} ms. Migration will continue.",
                resourceLabel,
                jobNo,
                resourceStopwatch.ElapsedMilliseconds);
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
