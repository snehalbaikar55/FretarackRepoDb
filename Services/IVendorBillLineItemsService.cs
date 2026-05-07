using GeneratedModels;

namespace RepoDbApi.Services;

public interface IVendorBillLineItemsService
{
    Task<IEnumerable<VendorBillLineItems>> GetByVendorBillIdAsync(int vendorBillId);
}
