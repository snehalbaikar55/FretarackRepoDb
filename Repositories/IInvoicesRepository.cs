using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IInvoicesRepository
{
    Task<IEnumerable<Invoices>> GetByCargoIdAsync(int cargoId);
}
