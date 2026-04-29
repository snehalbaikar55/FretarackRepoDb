using GeneratedModels;

namespace RepoDbApi.Services;

public interface IVwBillListService
{
    Task<IEnumerable<vw_BillList>> GetByCargoIdAsync(int cargoId);
}
