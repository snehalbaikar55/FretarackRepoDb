using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using RepoDbApi.Contracts;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/fretrack-migration")]
public class FretrackBulkHandledByController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FretrackBulkHandledByController> _logger;

    public FretrackBulkHandledByController(
        IConfiguration configuration,
        ILogger<FretrackBulkHandledByController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("bulk-handled-by-id")]
    public async Task<IActionResult> UpdateBulkHandledByAsync(
        [FromBody] BulkCargoHandledByUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.JobNos.Count == 0)
        {
            return BadRequest(CreateFailedResponse("Provide at least one jobNo."));
        }

        var sourceConnectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(sourceConnectionString))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, CreateFailedResponse("Connection string 'DefaultConnection' is not configured."));
        }

        var targetConnectionString = _configuration.GetConnectionString("CargoMintDB");
        if (string.IsNullOrWhiteSpace(targetConnectionString))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, CreateFailedResponse("Connection string 'CargoMintDB' is not configured."));
        }

        await using var sourceConnection = new SqlConnection(sourceConnectionString);
        await using var targetConnection = new SqlConnection(targetConnectionString);

        await sourceConnection.OpenAsync(cancellationToken);
        await targetConnection.OpenAsync(cancellationToken);

        var details = new List<BulkCargoHandledByUpdateItem>(request.JobNos.Count);
        using var transaction = targetConnection.BeginTransaction();
        var committed = false;

        try
        {
            foreach (var jobNo in request.JobNos.Where(jobNo => !string.IsNullOrWhiteSpace(jobNo)).Select(jobNo => jobNo.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var cargoId = await ResolveCargoIdAsync(sourceConnection, jobNo, cancellationToken);
                var handledById = await LoadHandledByAsync(sourceConnection, cargoId, cancellationToken);
                var stagingRowsUpdated = await UpdateCargoStagingHandledByAsync(
                    targetConnection,
                    transaction,
                    cargoId,
                    handledById,
                    cancellationToken);
                var shipmentRowsUpdated = await UpdateShipmentHandledByAsync(
                    targetConnection,
                    transaction,
                    cargoId,
                    cancellationToken);

                details.Add(new BulkCargoHandledByUpdateItem
                {
                    CargoId = cargoId,
                    JobNo = jobNo,
                    StagingRowsUpdated = stagingRowsUpdated,
                    ShipmentRowsUpdated = shipmentRowsUpdated
                });
            }

            await transaction.CommitAsync(cancellationToken);
            committed = true;

            return Ok(new BulkCargoHandledByUpdateResponse
            {
                Status = "success",
                Message = $"Updated handled-by values for {details.Count} cargo record(s).",
                Items = details,
                TotalStagingRowsUpdated = details.Sum(item => item.StagingRowsUpdated),
                TotalShipmentRowsUpdated = details.Sum(item => item.ShipmentRowsUpdated)
            });
        }
        catch (Exception ex)
        {
            if (!committed)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _logger.LogError(ex, "Bulk handled-by update failed.");
            var statusCode = ex is KeyNotFoundException ? StatusCodes.Status404NotFound : StatusCodes.Status500InternalServerError;

            return StatusCode(statusCode, CreateFailedResponse(ex.GetBaseException().Message));
        }
    }

    private static async Task<int> ResolveCargoIdAsync(
        SqlConnection sourceConnection,
        string jobNo,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(@"
SELECT CargoID
FROM dbo.Cargo
WHERE LTRIM(RTRIM(JobNo)) = LTRIM(RTRIM(@JobNo))
  AND ISNULL(isDeleted, 0) = 0;", sourceConnection);
        command.Parameters.Add(new SqlParameter("@JobNo", System.Data.SqlDbType.NVarChar, 100) { Value = jobNo });
        command.CommandTimeout = 600;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result is DBNull)
        {
            throw new KeyNotFoundException($"No cargo record found for JobNo '{jobNo}'.");
        }

        return Convert.ToInt32(result);
    }

    private static async Task<int?> LoadHandledByAsync(
        SqlConnection sourceConnection,
        int cargoId,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(@"
SELECT TOP (1) HandledBy
FROM dbo.CargoDetails
WHERE CargoID = @CargoID
  AND ISNULL(isDeleted, 0) = 0;", sourceConnection);
        command.Parameters.Add(new SqlParameter("@CargoID", System.Data.SqlDbType.Int) { Value = cargoId });
        command.CommandTimeout = 600;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result is DBNull)
        {
            return null;
        }

        return Convert.ToInt32(result);
    }

    private static Task<int> UpdateCargoStagingHandledByAsync(
        SqlConnection targetConnection,
        SqlTransaction transaction,
        int cargoId,
        int? handledById,
        CancellationToken cancellationToken)
    {
        return ExecuteNonQueryAsync(
            targetConnection,
            transaction,
            @"
UPDATE c
SET c.HandledById = COALESCE(@HandledById, c.HandledById),
    c.CargoMintHandledById = u.CargoMintUserID
FROM dbo.Fretrack_Cargo_Staging c
LEFT JOIN dbo.Fretrack_UserMaster_Staging u
    ON COALESCE(@HandledById, c.HandledById) = u.FretrackUserId
WHERE c.CargoId = @CargoID;",
            cargoId,
            handledById,
            cancellationToken);
    }

    private static Task<int> UpdateShipmentHandledByAsync(
        SqlConnection targetConnection,
        SqlTransaction transaction,
        int cargoId,
        CancellationToken cancellationToken)
    {
        return ExecuteNonQueryAsync(
            targetConnection,
            transaction,
            @"
UPDATE sh
SET sh.HandledById = c.CargoMintHandledById
FROM dbo.Shipments sh
JOIN dbo.Fretrack_Cargo_Staging c
    ON sh.FretrackCargoId = c.CargoId
WHERE c.CargoId = @CargoID;",
            cargoId,
            null,
            cancellationToken);
    }

    private static async Task<int> ExecuteNonQueryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        int cargoId,
        int? handledById,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandTimeout = 600
        };

        command.Parameters.Add(new SqlParameter("@CargoID", System.Data.SqlDbType.Int) { Value = cargoId });
        command.Parameters.Add(new SqlParameter("@HandledById", System.Data.SqlDbType.Int)
        {
            Value = handledById.HasValue ? handledById.Value : DBNull.Value
        });
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static BulkCargoHandledByUpdateResponse CreateFailedResponse(string message)
    {
        return new BulkCargoHandledByUpdateResponse
        {
            Status = "failed",
            Message = message
        };
    }
}
