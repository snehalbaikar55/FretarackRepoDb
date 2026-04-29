using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CargoContainersRepository : ICargoContainersRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoContainersRepository> _logger;

    public CargoContainersRepository(IConfiguration configuration, ILogger<CargoContainersRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoContainers>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoContainers>(
                "SELECT * FROM [CargoContainers] WHERE [CargoID] = @cargoId and [IsDeleted] = 0",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving cargo containers for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
