using System.ComponentModel.DataAnnotations;

namespace RepoDbApi.Contracts;

public sealed class SalesQuoteSyncRequest
{
    [Required, StringLength(20)]
    public string SalesQuoteNumber { get; set; } = string.Empty;
    [Range(1, int.MaxValue)]
    public int OrgId { get; set; }
    public bool DryRun { get; set; } = true;
    public bool ChargesOnly { get; set; } = true;
}

public sealed record SalesQuoteSyncResponse(
    string SalesQuoteNumber, int SalesQuoteID, string Status,
    bool DryRun, bool ActualMigrationCompleted, Dictionary<string, int> StagingCounts)
{
    public int OrgId { get; init; }
    public bool StagingMappingsUpdated { get; init; }
    public SalesQuotePrefillData? Quotation { get; init; }
    public SalesQuoteTableRows? Tables => Quotation is null ? null : SalesQuoteTableRows.From(Quotation);
    public string ImportScope { get; init; } = "FULL_QUOTATION";
    public List<SalesQuotePrefillLine> ChargeLines => Quotation?.SalesQuotationServiceDetails
        .SelectMany(service => service.SalesQuotationRateOptions)
        .SelectMany(option => option.SalesQuotationLineItems).ToList() ?? new();
    public bool RequiresReview => Quotation?.PrefillMissingFields.Count > 0;
}

public sealed class SalesQuoteSyncConflictException(string message) : Exception(message);

public sealed class SalesQuoteSyncStepException(string stage, Exception inner)
    : Exception($"Sales quote sync failed at {stage}.", inner)
{
    public string Stage { get; } = stage;
}

public sealed record SalesQuoteSyncFailure(string Message, string TraceId, string Stage,
    int? SqlErrorNumber, string? SqlProcedure, int? SqlLineNumber, string ErrorType);
