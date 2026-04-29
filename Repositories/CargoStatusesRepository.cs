using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CargoStatusesRepository : ICargoStatusesRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoStatusesRepository> _logger;

    public CargoStatusesRepository(IConfiguration configuration, ILogger<CargoStatusesRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoStatuses>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoStatuses>(
                @"SELECT cs.*,
                          COALESCE(umU.[UserDisplayName], umU.[UserName]) AS [UpdatedByName]
                  FROM [CargoStatuses] cs
                  LEFT JOIN [UserMaster] umU ON umU.[UserID] = cs.[ModifiedBy]
                  WHERE cs.[CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving CargoStatuses records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
