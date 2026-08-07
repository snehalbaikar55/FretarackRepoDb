using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services.CargoDocumentMigration;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/cargo-document-migration/by-cargo")]
public sealed class CargoDocumentMigrationByCargoController : ControllerBase
{
    private readonly ICargoDocumentMigrationService _migrationService;

    public CargoDocumentMigrationByCargoController(ICargoDocumentMigrationService migrationService)
    {
        _migrationService = migrationService;
    }

    [HttpPost("migrate/{cargoId:int}/{fretrackCargoDocumentId:int}")]
    public async Task<IActionResult> Migrate(
        int cargoId,
        int fretrackCargoDocumentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _migrationService.MigrateAsync(cargoId, fretrackCargoDocumentId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = ex.Message });
        }
    }
}
