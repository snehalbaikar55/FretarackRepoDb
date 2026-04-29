using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class OceanShipmentRoutingRepository : IOceanShipmentRoutingRepository
{
    private readonly string _connectionString;
    private readonly ILogger<OceanShipmentRoutingRepository> _logger;

    public OceanShipmentRoutingRepository(IConfiguration configuration, ILogger<OceanShipmentRoutingRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<OceanShipmentRouting>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<OceanShipmentRouting>(
                "SELECT * FROM [OceanShipmentRouting] WHERE [ShipmentID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving ocean shipment routing for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
