using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/cost-sheet-charges")]
public class CostSheetChargesController : ControllerBase
{
    private readonly ICostSheetChargesService _service;

    public CostSheetChargesController(ICostSheetChargesService service)
    {
        _service = service;
    }

    [HttpGet("by-cargo/{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        return Ok(await _service.GetByCargoIdAsync(cargoId));
    }

    [HttpGet("by-costsheet/{costSheetId:int}")]
    public async Task<IActionResult> GetByCostSheetId(int costSheetId)
    {
        return Ok(await _service.GetByCostSheetIdAsync(costSheetId));
    }
}
