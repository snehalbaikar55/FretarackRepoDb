using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IInvoiceLineItemsRepository
{
    Task<IEnumerable<InvoiceLineItems>> GetByInvoiceIdAsync(int invoiceId);
}
