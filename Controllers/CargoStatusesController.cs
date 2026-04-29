using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/cargo-statuses")]
public class CargoStatusesController : ControllerBase
{
    private readonly ICargoStatusesService _service;

    public CargoStatusesController(ICargoStatusesService service)
    {
        _service = service;
    }

    [HttpGet("by-cargo/{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        return Ok(await _service.GetByCargoIdAsync(cargoId));
    }
}
