using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/air-shipment-routing")]
public class AirShipmentRoutingController : ControllerBase
{
    private readonly IAirShipmentRoutingService _service;

    public AirShipmentRoutingController(IAirShipmentRoutingService service)
    {
        _service = service;
    }

    [HttpGet("by-cargo/{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        return Ok(await _service.GetByCargoIdAsync(cargoId));
    }
}
