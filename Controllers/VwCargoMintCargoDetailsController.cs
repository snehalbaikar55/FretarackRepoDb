using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/vw-cargo-mint-cargo-details")]
public class VwCargoMintCargoDetailsController : ControllerBase
{
    private readonly IVwCargoMintCargoDetailsService _service;

    public VwCargoMintCargoDetailsController(IVwCargoMintCargoDetailsService service)
    {
        _service = service;
    }

    [HttpGet("by-cargo/{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        var result = await _service.GetByCargoIdAsync(cargoId);
        return Ok(result);
    }
}
