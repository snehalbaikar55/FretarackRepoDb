using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Contracts;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/fretrack-migration")]
public class FretrackMigrationController : ControllerBase
{
    private readonly ICargoMigrationService _service;
    private readonly ILogger<FretrackMigrationController> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public FretrackMigrationController(
        ICargoMigrationService service,
        ILogger<FretrackMigrationController> logger,
        IHostEnvironment hostEnvironment)
    {
        _service = service;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    [HttpPost("single-job")]
    public async Task<IActionResult> MigrateSingleJob([FromBody] CargoMigrationRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.JobNo))
        {
            return BadRequest("JobNo is required.");
        }

        try
        {
            var result = await _service.MigrateSingleJobAsync(request.JobNo.Trim(), cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Cargo record not found for JobNo: {JobNo}", request.JobNo);
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Migration setup error for JobNo: {JobNo}", request.JobNo);
            return Problem(
                title: "Migration configuration error.",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate cargo for JobNo: {JobNo}", request.JobNo);
            if (_hostEnvironment.IsDevelopment())
            {
                return Problem(
                    title: "Failed to migrate single cargo.",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while migrating single cargo.");
        }
    }
}
