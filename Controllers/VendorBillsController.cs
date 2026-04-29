using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/vendor-bills")]
public class VendorBillsController : ControllerBase
{
    private readonly IVendorBillsService _service;

    public VendorBillsController(IVendorBillsService service)
    {
        _service = service;
    }

    [HttpGet("by-cargo/{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        return Ok(await _service.GetByCargoIdAsync(cargoId));
    }
}
