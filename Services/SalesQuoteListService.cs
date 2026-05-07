using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class SalesQuoteListService : ISalesQuoteListService
{
    private readonly ISalesQuoteListRepository _repository;

    public SalesQuoteListService(ISalesQuoteListRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<SalesQuoteList>> GetAllAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? salesQuoteNumber = null,
        string? quoteType = null,
        string? customer = null,
        string? contact = null,
        string? salesPerson = null,
        string? salesQuoteStatus = null)
    {
        return await _repository.GetAllAsync(
            fromDate,
            toDate,
            salesQuoteNumber,
            quoteType,
            customer,
            contact,
            salesPerson,
            salesQuoteStatus);
    }

    public async Task<GeneratedModels.vw_cargoMint_salesQuote_Detail?> GetByIdAsync(int salesQuoteId)
    {
        return await _repository.GetByIdAsync(salesQuoteId);
    }

    public async Task<IEnumerable<GeneratedModels.SalesQuoteDetails_New>> GetDetailsBySalesQuoteIdAsync(int salesQuoteId)
    {
        return await _repository.GetDetailsBySalesQuoteIdAsync(salesQuoteId);
    }

    public async Task<IEnumerable<GeneratedModels.SalesQuoteCharges_New>> GetIncomeChargesBySalesQuoteIdAsync(int salesQuoteId)
    {
        return await _repository.GetIncomeChargesBySalesQuoteIdAsync(salesQuoteId);
    }
}
