using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class VendorBillLineItemsRepository : IVendorBillLineItemsRepository
{
    private readonly string _connectionString;
    private readonly ILogger<VendorBillLineItemsRepository> _logger;

    public VendorBillLineItemsRepository(IConfiguration configuration, ILogger<VendorBillLineItemsRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<VendorBillLineItems>> GetByVendorBillIdAsync(int vendorBillId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<VendorBillLineItems>(
                @"SELECT *
                  FROM [VendorBillLineItems]
                  WHERE [VendorBillID] = @vendorBillId
                  ORDER BY [SortOrder], [VendorBillLineItemID]",
                new { vendorBillId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving vendor bill line items for VendorBillID: {VendorBillId}", vendorBillId);
            throw;
        }
    }
}
