using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AllCargoListController : ControllerBase
{
    private readonly IAllCargoListService _allCargoListService;
    private readonly ILogger<AllCargoListController> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public AllCargoListController(
        IAllCargoListService allCargoListService,
        ILogger<AllCargoListController> logger,
        IHostEnvironment hostEnvironment)
    {
        _allCargoListService = allCargoListService;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? createdFrom = null, [FromQuery] DateTime? createdTo = null, [FromQuery] string? job = null, [FromQuery] string? cycle = null)
    {
        try
        {
            var result = await _allCargoListService.GetAllAsync(createdFrom, createdTo, job, cycle);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid date filter for AllCargoList.");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch AllCargoList records.");
            if (_hostEnvironment.IsDevelopment())
            {
                return Problem(
                    title: "Failed to fetch AllCargoList records.",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while fetching AllCargoList records.");
        }
    }
}
