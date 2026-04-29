using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CargoDetailsRepository : ICargoDetailsRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoDetailsRepository> _logger;

    public CargoDetailsRepository(IConfiguration configuration, ILogger<CargoDetailsRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoDetails>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoDetails>(
                "SELECT * FROM [CargoDetails] WHERE [CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving CargoDetails records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
