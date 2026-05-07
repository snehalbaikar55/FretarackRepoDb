using RepoDbApi.Models;

namespace RepoDbApi.Repositories;

public interface ISalesQuoteChargesRepository
{
    Task<IEnumerable<CargoMintSalesQuoteCharge>> GetBySalesQuoteIdAsync(int salesQuoteId);
}
