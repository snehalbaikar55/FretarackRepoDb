using RepoDbApi.Models;

namespace RepoDbApi.Services;

public interface ISalesQuoteChargesService
{
    Task<IEnumerable<CargoMintSalesQuoteCharge>> GetBySalesQuoteIdAsync(int salesQuoteId);
}
