using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class VwBillListService : IVwBillListService
{
    private readonly IVwBillListRepository _repository;

    public VwBillListService(IVwBillListRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<vw_BillList>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
