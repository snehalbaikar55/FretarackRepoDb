using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class InvoiceLineItemsService : IInvoiceLineItemsService
{
    private readonly IInvoiceLineItemsRepository _repository;

    public InvoiceLineItemsService(IInvoiceLineItemsRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<InvoiceLineItems>> GetByInvoiceIdAsync(int invoiceId)
    {
        return await _repository.GetByInvoiceIdAsync(invoiceId);
    }
}
