using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/ocean-shipment-routing")]
public class OceanShipmentRoutingController : ControllerBase
{
    private readonly IOceanShipmentRoutingService _service;

    public OceanShipmentRoutingController(IOceanShipmentRoutingService service)
    {
        _service = service;
    }

    [HttpGet("by-cargo/{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        return Ok(await _service.GetByCargoIdAsync(cargoId));
    }
}
