using System.Text.Json.Serialization;

namespace RepoDbApi.Contracts;

// Compact response for CHARGES_ONLY; the full response remains an explicit opt-in.
public sealed class SalesQuoteChargesResponse
{
    public string SalesQuoteNumber { get; init; } = string.Empty;
    public int OrgId { get; init; }
    public string ImportScope => "CHARGES_ONLY";
    public int? ServiceTypeId { get; init; }
    public string? ServiceLabel { get; init; }
    public int? TransportModeId { get; init; }
    public string? TransportMode { get; init; }
    public int? TransportDirectionId { get; init; }
    public string? TransportDirection { get; init; }
    public int? CargoMintPOLID { get; init; }
    public int? CargoMintPODID { get; init; }
    public List<SalesQuoteChargeInput> ChargeLines { get; init; } = new();

    public static SalesQuoteChargesResponse From(SalesQuoteSyncResponse result)
    {
        var contexts = (result.Quotation?.SalesQuotationServiceDetails ?? new())
            .Select(s => new { s.ServiceTypeId, ServiceLabel = SalesQuoteServiceNames.Label(s.ServiceLabel, s.TransportMode), s.TransportModeId,
                s.TransportMode, s.TransportDirectionId, s.TransportDirection, s.PolId, s.PodId })
            .Distinct().ToList();
        if (contexts.Count > 1)
            throw new SalesQuoteSyncConflictException("The compact charge response requires one service/route context. Use chargesOnly=false for a multi-service quotation.");
        var service = contexts.SingleOrDefault();
        return new()
        {
            SalesQuoteNumber = result.SalesQuoteNumber, OrgId = result.OrgId,
            ServiceTypeId = service?.ServiceTypeId, ServiceLabel = service?.ServiceLabel,
            TransportModeId = service?.TransportModeId, TransportMode = service?.TransportMode,
            TransportDirectionId = service?.TransportDirectionId, TransportDirection = service?.TransportDirection,
            CargoMintPOLID = service?.PolId, CargoMintPODID = service?.PodId,
            ChargeLines = result.ChargeLines.Select(line => new SalesQuoteChargeInput
            {
                OrgId = line.OrgId, ChargeItemId = line.ChargeItemId, ChargeDescription = line.ChargeDescription,
                SellCurrencyId = line.SellCurrencyId, SellExRate = line.SellExRate, SellRate = line.SellRate, SellQuantity = line.SellQuantity,
                BuyCurrencyId = line.BuyCurrencyId, BuyExRate = line.BuyExRate, BuyRate = line.BuyRate, BuyQuantity = line.BuyQuantity,
                BuyTaxPercent = line.BuyTaxPercent,
                ApplyPerId = line.ApplyPerId, HsnCode = line.HsnCode,
                SellTaxId = line.SellTaxId, SellTaxPercent = line.SellTaxPercent, BuyTaxId = line.BuyTaxId,
                InvoiceToId = line.InvoiceToId, BillToId = line.BillToId, SortOrder = line.SortOrder,
                IsAllIn = line.IsAllIn ? true : null, Remarks = line.Remarks
            }).ToList()
        };
    }
}

public sealed class SalesQuoteChargeInput
{
    public int OrgId { get; init; }
    public int? ChargeItemId { get; init; }
    public string? ChargeDescription { get; init; }
    public int? SellCurrencyId { get; init; }
    public decimal? SellExRate { get; init; }
    public decimal? SellRate { get; init; }
    public decimal? SellQuantity { get; init; }
    public int? BuyCurrencyId { get; init; }
    public decimal? BuyExRate { get; init; }
    public decimal? BuyRate { get; init; }
    public decimal? BuyQuantity { get; init; }
    public decimal? BuyTaxPercent { get; init; }

    // Preserve usable charge inputs without sending null optional fields,
    // generated IDs, audit columns or calculated quotation totals.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ApplyPerId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HsnCode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SellTaxId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? SellTaxPercent { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BuyTaxId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? InvoiceToId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BillToId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SortOrder { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsAllIn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Remarks { get; init; }
}
