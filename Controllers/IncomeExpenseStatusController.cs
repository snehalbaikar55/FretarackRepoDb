using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncomeExpenseStatusController : ControllerBase
{
    private readonly IIncomeExpenseStatusService _service;
    private readonly ILogger<IncomeExpenseStatusController> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public IncomeExpenseStatusController(
        IIncomeExpenseStatusService service,
        ILogger<IncomeExpenseStatusController> logger,
        IHostEnvironment hostEnvironment)
    {
        _service = service;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> GetByJobNo([FromQuery] string jobNo)
    {
        if (string.IsNullOrWhiteSpace(jobNo))
        {
            return BadRequest("'jobNo' is required.");
        }

        try
        {
            var result = await _service.GetByJobNoAsync(jobNo.Trim());
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch IncomeExpenseStatus records for JobNo {JobNo}.", jobNo);
            if (_hostEnvironment.IsDevelopment())
            {
                return Problem(
                    title: "Failed to fetch IncomeExpenseStatus records.",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while fetching IncomeExpenseStatus records.");
        }
    }
}
