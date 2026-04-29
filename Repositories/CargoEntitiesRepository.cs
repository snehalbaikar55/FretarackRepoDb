using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CargoEntitiesRepository : ICargoEntitiesRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoEntitiesRepository> _logger;

    public CargoEntitiesRepository(IConfiguration configuration, ILogger<CargoEntitiesRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoEntities>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoEntities>(
                @"SELECT ce.*,
                         c.[CompanyName] AS [CompanyName]
                  FROM [CargoEntities] ce
                  LEFT JOIN [Companies] c ON c.[CompanyID] = ce.[CompanyID]
                  WHERE ce.[CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving CargoEntities records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
