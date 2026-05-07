using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class VendorBillLineItemsService : IVendorBillLineItemsService
{
    private readonly IVendorBillLineItemsRepository _repository;

    public VendorBillLineItemsService(IVendorBillLineItemsRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<VendorBillLineItems>> GetByVendorBillIdAsync(int vendorBillId)
    {
        return await _repository.GetByVendorBillIdAsync(vendorBillId);
    }
}
