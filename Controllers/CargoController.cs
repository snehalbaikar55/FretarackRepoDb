using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CargoController : ControllerBase
{
    private readonly ICargoService _cargoService;
    private readonly ILogger<CargoController> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public CargoController(
        ICargoService cargoService,
        ILogger<CargoController> logger,
        IHostEnvironment hostEnvironment)
    {
        _cargoService = cargoService;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCargo()
    {
        try
        {
            var result = await _cargoService.GetAllCargoAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Cargo records.");
            if (_hostEnvironment.IsDevelopment())
            {
                return Problem(
                    title: "Failed to fetch Cargo records.",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while fetching Cargo records.");
        }
    }
}
