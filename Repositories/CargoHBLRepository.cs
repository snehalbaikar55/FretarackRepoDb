using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CargoHBLRepository : ICargoHBLRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoHBLRepository> _logger;

    public CargoHBLRepository(IConfiguration configuration, ILogger<CargoHBLRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoHBL>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoHBL>(
                @"SELECT ch.*,
                         COALESCE(um.[UserDisplayName], um.[UserName]) AS [CreatedByName]
                  FROM [CargoHBL] ch
                  LEFT JOIN [UserMaster] um ON um.[UserID] = ch.[CreatedBy]
                  WHERE ch.[CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving CargoHBL records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
