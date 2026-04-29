using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IVwBillListRepository
{
    Task<IEnumerable<vw_BillList>> GetByCargoIdAsync(int cargoId);
}
