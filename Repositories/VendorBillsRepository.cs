using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class VendorBillsRepository : IVendorBillsRepository
{
    private readonly string _connectionString;
    private readonly ILogger<VendorBillsRepository> _logger;

    public VendorBillsRepository(IConfiguration configuration, ILogger<VendorBillsRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<VendorBill>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<VendorBill>(
                @"SELECT vb.*,
                         COALESCE(um.[UserDisplayName], um.[UserName]) AS [CreatedByName]
                  FROM [VendorBill] vb
                  LEFT JOIN [UserMaster] um ON um.[UserID] = vb.[CreatedBy]
                  WHERE vb.[CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving vendor bills for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
