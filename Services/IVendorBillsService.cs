using GeneratedModels;

namespace RepoDbApi.Services;

public interface IVendorBillsService
{
    Task<IEnumerable<VendorBill>> GetByCargoIdAsync(int cargoId);
}
