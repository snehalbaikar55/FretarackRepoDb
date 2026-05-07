using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IVendorBillLineItemsRepository
{
    Task<IEnumerable<VendorBillLineItems>> GetByVendorBillIdAsync(int vendorBillId);
}
