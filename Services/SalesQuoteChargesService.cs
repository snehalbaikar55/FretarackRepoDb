using RepoDbApi.Models;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class SalesQuoteChargesService : ISalesQuoteChargesService
{
    private readonly ISalesQuoteChargesRepository _repository;
    private readonly ILogger<SalesQuoteChargesService> _logger;

    public SalesQuoteChargesService(ISalesQuoteChargesRepository repository, ILogger<SalesQuoteChargesService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<CargoMintSalesQuoteCharge>> GetBySalesQuoteIdAsync(int salesQuoteId)
    {
        try
        {
            return await _repository.GetBySalesQuoteIdAsync(salesQuoteId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving sales quote charges for SalesQuoteID: {SalesQuoteId}", salesQuoteId);
            throw;
        }
    }
}
