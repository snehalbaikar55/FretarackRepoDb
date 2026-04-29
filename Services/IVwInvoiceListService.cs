using GeneratedModels;

namespace RepoDbApi.Services;

public interface IVwInvoiceListService
{
    Task<IEnumerable<vw_InvoiceList>> GetByCargoIdAsync(int cargoId);
}
