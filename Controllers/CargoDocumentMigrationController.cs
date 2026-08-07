using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services.CargoDocumentMigration;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/cargo-document-migration")]
public sealed class CargoDocumentMigrationController : ControllerBase
{
    private readonly ICargoDocumentMigrationService _migrationService;

    public CargoDocumentMigrationController(ICargoDocumentMigrationService migrationService)
    {
        _migrationService = migrationService;
    }

    [HttpPost("migrate/{fretrackCargoDocumentId:int}")]
    public async Task<IActionResult> Migrate(int fretrackCargoDocumentId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _migrationService.MigrateAsync(fretrackCargoDocumentId, cancellationToken);
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
