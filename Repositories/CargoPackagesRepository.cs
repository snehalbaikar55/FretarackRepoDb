using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CargoPackagesRepository : ICargoPackagesRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoPackagesRepository> _logger;

    public CargoPackagesRepository(IConfiguration configuration, ILogger<CargoPackagesRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoPackages>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoPackages>(
                "SELECT * FROM [CargoPackages] WHERE [CargoID] = @cargoId and [IsDeleted] = 0",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving cargo packages for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
