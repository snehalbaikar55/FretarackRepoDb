using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IVendorBillsRepository
{
    Task<IEnumerable<VendorBill>> GetByCargoIdAsync(int cargoId);
}
