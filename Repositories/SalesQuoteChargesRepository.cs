using Microsoft.Data.SqlClient;
using RepoDb;
using RepoDbApi.Models;

namespace RepoDbApi.Repositories;

public class SalesQuoteChargesRepository : ISalesQuoteChargesRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SalesQuoteChargesRepository> _logger;

    public SalesQuoteChargesRepository(IConfiguration configuration, ILogger<SalesQuoteChargesRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoMintSalesQuoteCharge>> GetBySalesQuoteIdAsync(int salesQuoteId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoMintSalesQuoteCharge>(
                "SELECT * FROM [vw_CargoMint_SalesQuoteCharges] WHERE [SQID] = @salesQuoteId",
                new { salesQuoteId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving vw_CargoMint_SalesQuoteCharges for SalesQuoteID: {SalesQuoteId}", salesQuoteId);
            throw;
        }
    }
}
