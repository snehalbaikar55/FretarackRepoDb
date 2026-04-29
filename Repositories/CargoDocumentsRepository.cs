using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CargoDocumentsRepository : ICargoDocumentsRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoDocumentsRepository> _logger;

    public CargoDocumentsRepository(IConfiguration configuration, ILogger<CargoDocumentsRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoDocuments>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoDocuments>(
                @"SELECT cd.*,
                         COALESCE(um.[UserDisplayName], um.[UserName]) AS [CreatedByName]
                  FROM [CargoDocuments] cd
                  LEFT JOIN [UserMaster] um ON um.[UserID] = cd.[CreatedBy]
                  WHERE cd.[CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving CargoDocuments records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
