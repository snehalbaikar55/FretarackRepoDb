using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/cargo-awb")]
public class CargoAWBController : ControllerBase
{
    private readonly ICargoAWBService _service;

    public CargoAWBController(ICargoAWBService service)
    {
        _service = service;
    }

    [HttpGet("by-cargo/{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        return Ok(await _service.GetByCargoIdAsync(cargoId));
    }
}
