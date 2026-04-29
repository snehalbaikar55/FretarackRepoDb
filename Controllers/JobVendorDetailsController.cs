using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/job-vendor-details")]
public class JobVendorDetailsController : ControllerBase
{
    private readonly IJobVendorDetailsService _service;

    public JobVendorDetailsController(IJobVendorDetailsService service)
    {
        _service = service;
    }

    [HttpGet("by-cargo/{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        return Ok(await _service.GetByCargoIdAsync(cargoId));
    }
}
