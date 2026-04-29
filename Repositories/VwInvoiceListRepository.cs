using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class VwInvoiceListRepository : IVwInvoiceListRepository
{
    private readonly string _connectionString;
    private readonly ILogger<VwInvoiceListRepository> _logger;

    public VwInvoiceListRepository(IConfiguration configuration, ILogger<VwInvoiceListRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<vw_InvoiceList>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<vw_InvoiceList>(
                @"SELECT vil.*,
                         COALESCE(um.[UserDisplayName], um.[UserName]) AS [CreatedByName]
                  FROM [vw_InvoiceList] vil
                  LEFT JOIN [UserMaster] um ON um.[UserID] = vil.[CreatedBy]
                  WHERE vil.[CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving vw_InvoiceList records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
