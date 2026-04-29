using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CostSheetsRepository : ICostSheetsRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CostSheetsRepository> _logger;

    public CostSheetsRepository(IConfiguration configuration, ILogger<CostSheetsRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CostSheet>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CostSheet>(
                @"SELECT cs.*,
                          COALESCE(umC.[UserDisplayName], umC.[UserName]) AS [CreatedByName],
                          COALESCE(umM.[UserDisplayName], umM.[UserName]) AS [ModifiedByName]
                  FROM [CostSheet] cs
                  LEFT JOIN [UserMaster] umC ON umC.[UserID] = cs.[CreatedBy]
                  LEFT JOIN [UserMaster] umM ON umM.[UserID] = cs.[ModifiedBy]
                  WHERE cs.[CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving cost sheets for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
