using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CargoCANRepository : ICargoCANRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoCANRepository> _logger;

    public CargoCANRepository(IConfiguration configuration, ILogger<CargoCANRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoCAN>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoCAN>(
                "SELECT * FROM [CargoCAN] WHERE [cargoid] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving CargoCAN records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
