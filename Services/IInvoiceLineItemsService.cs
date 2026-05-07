using GeneratedModels;

namespace RepoDbApi.Services;

public interface IInvoiceLineItemsService
{
    Task<IEnumerable<InvoiceLineItems>> GetByInvoiceIdAsync(int invoiceId);
}
