using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class VwBillListRepository : IVwBillListRepository
{
    private readonly string _connectionString;
    private readonly ILogger<VwBillListRepository> _logger;

    public VwBillListRepository(IConfiguration configuration, ILogger<VwBillListRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<vw_BillList>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<vw_BillList>(
                @"SELECT vbl.*,
                         COALESCE(um.[UserDisplayName], um.[UserName]) AS [CreatedByName]
                  FROM [vw_BillList] vbl
                  LEFT JOIN [UserMaster] um ON um.[UserID] = vbl.[CreatedBy]
                  WHERE vbl.[CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving vw_BillList records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
