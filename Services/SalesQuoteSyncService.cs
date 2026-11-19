using RepoDbApi.Contracts;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public interface ISalesQuoteSyncService
{
    Task<SalesQuoteSyncResponse> SyncAsync(SalesQuoteSyncRequest request, CancellationToken cancellationToken);
}

public sealed class SalesQuoteSyncService(ISalesQuoteSyncRepository repository) : ISalesQuoteSyncService
{
    public async Task<SalesQuoteSyncResponse> SyncAsync(SalesQuoteSyncRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SalesQuoteNumber))
            throw new ArgumentException("SalesQuoteNumber is required.");
        if (request.OrgId <= 0)
            throw new ArgumentException("A positive OrgId is required.");
        var result = await repository.SyncAsync(request.SalesQuoteNumber.Trim(), request.OrgId, request.DryRun, cancellationToken);
        if (request.ChargesOnly && result.Quotation is not null)
        {
            result.Quotation.PrefillMissingFields = result.Quotation.PrefillMissingFields
                .Where(field => field.Key.Contains(".salesQuotationLineItems[", StringComparison.Ordinal))
                .ToDictionary(field => field.Key, field => field.Value);
        }
        return result with { ImportScope = request.ChargesOnly ? "CHARGES_ONLY" : "FULL_QUOTATION" };
    }
}
