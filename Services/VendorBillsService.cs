using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class VendorBillsService : IVendorBillsService
{
    private readonly IVendorBillsRepository _repository;

    public VendorBillsService(IVendorBillsRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<VendorBill>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
