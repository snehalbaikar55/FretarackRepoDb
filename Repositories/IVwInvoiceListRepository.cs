using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IVwInvoiceListRepository
{
    Task<IEnumerable<vw_InvoiceList>> GetByCargoIdAsync(int cargoId);
}
