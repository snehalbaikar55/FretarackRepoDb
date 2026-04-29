using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class VwInvoiceListService : IVwInvoiceListService
{
    private readonly IVwInvoiceListRepository _repository;

    public VwInvoiceListService(IVwInvoiceListRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<vw_InvoiceList>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
