using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ISalesQuoteListRepository
{
    Task<IEnumerable<SalesQuoteList>> GetAllAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? salesQuoteNumber = null,
        string? quoteType = null,
        string? customer = null,
        string? contact = null,
        string? salesPerson = null,
        string? salesQuoteStatus = null);

    Task<GeneratedModels.vw_cargoMint_salesQuote_Detail?> GetByIdAsync(int salesQuoteId);
    Task<IEnumerable<GeneratedModels.SalesQuoteDetails_New>> GetDetailsBySalesQuoteIdAsync(int salesQuoteId);
    Task<IEnumerable<GeneratedModels.SalesQuoteCharges_New>> GetIncomeChargesBySalesQuoteIdAsync(int salesQuoteId);
}
