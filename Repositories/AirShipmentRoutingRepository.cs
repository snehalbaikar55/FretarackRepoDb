using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class AirShipmentRoutingRepository : IAirShipmentRoutingRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AirShipmentRoutingRepository> _logger;

    public AirShipmentRoutingRepository(IConfiguration configuration, ILogger<AirShipmentRoutingRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<AirShipmentRouting>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<AirShipmentRouting>(
                "SELECT * FROM [AirShipmentRouting] WHERE [ShipmentID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving air shipment routing for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
