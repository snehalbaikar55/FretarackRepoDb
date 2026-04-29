using GeneratedModels;

namespace RepoDbApi.Services;

public interface IInvoicesService
{
    Task<IEnumerable<Invoices>> GetByCargoIdAsync(int cargoId);
}
