using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class InvoiceLineItemsRepository : IInvoiceLineItemsRepository
{
    private readonly string _connectionString;
    private readonly ILogger<InvoiceLineItemsRepository> _logger;

    public InvoiceLineItemsRepository(IConfiguration configuration, ILogger<InvoiceLineItemsRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<InvoiceLineItems>> GetByInvoiceIdAsync(int invoiceId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<InvoiceLineItems>(
                @"SELECT *
                  FROM [InvoiceLineItems]
                  WHERE [InvoiceID] = @invoiceId
                    AND [isDeleted] = 0
                  ORDER BY [SortOrder], [InvoiceLineItemID]",
                new { invoiceId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving invoice line items for InvoiceID: {InvoiceId}", invoiceId);
            throw;
        }
    }
}
