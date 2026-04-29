using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CargoChargesRepository : ICargoChargesRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoChargesRepository> _logger;

    public CargoChargesRepository(IConfiguration configuration, ILogger<CargoChargesRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoCharges>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoCharges>(
                "SELECT * FROM [CargoCharges] WHERE [CostSheetID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving cargo charges for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
