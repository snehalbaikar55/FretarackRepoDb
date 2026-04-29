using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CargoAWBRepository : ICargoAWBRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoAWBRepository> _logger;

    public CargoAWBRepository(IConfiguration configuration, ILogger<CargoAWBRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoAWB>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoAWB>(
                @"SELECT ca.*,
                         COALESCE(um.[UserDisplayName], um.[UserName]) AS [CreatedByName]
                  FROM [CargoAWB] ca
                  LEFT JOIN [UserMaster] um ON um.[UserID] = ca.[CreatedBy]
                  WHERE ca.[CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving CargoAWB records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
