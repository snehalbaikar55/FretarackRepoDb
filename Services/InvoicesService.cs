using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class InvoicesService : IInvoicesService
{
    private readonly IInvoicesRepository _repository;

    public InvoicesService(IInvoicesRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<Invoices>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
