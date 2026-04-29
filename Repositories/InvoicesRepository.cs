using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class InvoicesRepository : IInvoicesRepository
{
    private readonly string _connectionString;
    private readonly ILogger<InvoicesRepository> _logger;

    public InvoicesRepository(IConfiguration configuration, ILogger<InvoicesRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<Invoices>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<Invoices>(
                @"SELECT i.*,
                         COALESCE(um.[UserDisplayName], um.[UserName]) AS [CreatedByName]
                  FROM [Invoices] i
                  LEFT JOIN [UserMaster] um ON um.[UserID] = i.[CreatedBy]
                  WHERE i.[CargoID] = @cargoId and i.[IsDeleted] = 0",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving invoices for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
