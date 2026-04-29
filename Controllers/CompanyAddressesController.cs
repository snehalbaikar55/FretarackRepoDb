using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/company-addresses")]
public class CompanyAddressesController : ControllerBase
{
    private readonly ICompanyAddressesService _service;

    public CompanyAddressesController(ICompanyAddressesService service)
    {
        _service = service;
    }

    [HttpGet("by-cargo/{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        return Ok(await _service.GetByCargoIdAsync(cargoId));
    }
}
