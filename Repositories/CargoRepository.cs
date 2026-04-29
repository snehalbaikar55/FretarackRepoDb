using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CargoRepository : ICargoRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoRepository> _logger;

    public CargoRepository(IConfiguration configuration, ILogger<CargoRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<Cargo>> GetAllAsync()
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAllAsync<Cargo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving all Cargo records.");
            throw;
        }
    }
}
