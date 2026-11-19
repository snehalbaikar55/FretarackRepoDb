namespace RepoDbApi.Contracts;

// Matches CargoMint SalesQuotationSaveDto / patchQuotationForm's JSON names.
public sealed class SalesQuotePrefillData
{
    public int? QuotationId { get; set; }
    public int OrgId { get; set; }
    public int? EnquiryId { get; set; }
    public string? QuotationNo { get; set; }
    public DateTime? QuotationDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ValidityDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string QuotationStatus { get; set; } = "QUOTATION_DRAFT";
    public string? NominationType { get; set; }
    public int? CustomerId { get; set; }
    public string? CompanyName { get; set; }
    public int? ContactId { get; set; }
    public string? ContactName { get; set; }
    public int? SalesPersonId { get; set; }
    public string? SalesPersonName { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedBy { get; set; }
    public DateTime? DeletedDate { get; set; }
    public string? PreparedByName { get; set; }
    public int? CurrencyId { get; set; }
    public string? CurrencyCode { get; set; }
    public int? LocalCurrencyId { get; set; }
    public decimal? ExRate { get; set; }
    public int? IncoTermId { get; set; }
    public int? CommodityId { get; set; }
    public string? CommodityName { get; set; }
    public string? TermsConditions { get; set; }
    public string? OverallComments { get; set; }
    public bool? IsHazCargo { get; set; }
    public bool ShowTax { get; set; }
    public bool ShowTerms { get; set; } = true;
    public bool ShowCarrier { get; set; }
    public string PresentationMode { get; set; } = "SERVICE_WISE";
    public string ChargeGroupingMode { get; set; } = "SERVICE_OPTION";
    public List<object> PresentationCombinations { get; set; } = new();
    public decimal? TotalApprovedCostRate { get; set; }
    public decimal? TotalFinalSalesRate { get; set; }
    public decimal? TotalProfit { get; set; }
    public decimal? TotalProfitPercentage { get; set; }
    public decimal? TotalApprovedCostRateLocal { get; set; }
    public decimal? TotalFinalSalesRateLocal { get; set; }
    public decimal? TotalProfitLocal { get; set; }
    public string? PresentationCombinationsJson { get; set; }
    public DateTime? PresentationLockedDate { get; set; }
    public Dictionary<string, List<string>> PrefillMissingFields { get; set; } = new();
    public List<SalesQuotePrefillService> SalesQuotationServiceDetails { get; set; } = new();
}

public sealed class SalesQuotePrefillService
{
    public int? QuotationServiceId { get; set; }
    public int? QuotationId { get; set; }
    public int OrgId { get; set; }
    public int? EnquiryServiceId { get; set; }
    public int? BuyingRateId { get; set; }
    public int? RateRequestId { get; set; }
    public string? SalesQuoteServiceRefNo { get; set; }
    public int? ServiceLocationId { get; set; }
    public int? CurrencyId { get; set; }
    public decimal? ExRate { get; set; }
    public int? LocalCurrencyId { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedBy { get; set; }
    public DateTime? DeletedDate { get; set; }
    public int? ServiceTypeId { get; set; }
    public string? ServiceLabel { get; set; }
    public string? ServiceStatus { get; set; }
    public string? ServiceNotes { get; set; }
    public int? TransportModeId { get; set; }
    public string? TransportMode { get; set; }
    public int? TransportDirectionId { get; set; }
    public string? TransportDirection { get; set; }
    public int? PolId { get; set; }
    public string? Pol { get; set; }
    public int? PodId { get; set; }
    public string? Pod { get; set; }
    public bool IsPrimary { get; set; }
    public decimal? ApprovedCostRate { get; set; }
    public decimal? FinalSalesRate { get; set; }
    public decimal? ApprovedCostRateLocal { get; set; }
    public decimal? FinalSalesRateLocal { get; set; }
    public List<object> SalesQuotationContainers { get; set; } = new();
    public List<object> SalesQuotationPackages { get; set; } = new();
    public List<SalesQuotePrefillOption> SalesQuotationRateOptions { get; set; } = new();
}

public sealed class SalesQuotePrefillOption
{
    public int? QuotationOptionId { get; set; }
    public int? QuotationServiceId { get; set; }
    public int OrgId { get; set; }
    public string OptionLabel { get; set; } = "Imported from Fretrack";
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public int? CarrierId { get; set; }
    public string? CarrierName { get; set; }
    public string? OptionStatus { get; set; }
    public string? OptionNotes { get; set; }
    public int? BuyingRateId { get; set; }
    public string? CustomerOptionLabel { get; set; }
    public string? CustomerTag { get; set; }
    public int? CurrencyId { get; set; }
    public decimal? ExRate { get; set; }
    public int? LocalCurrencyId { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedBy { get; set; }
    public DateTime? DeletedDate { get; set; }
    public decimal? ApprovedCostRate { get; set; }
    public decimal? FinalSalesRate { get; set; }
    public List<SalesQuotePrefillLine> SalesQuotationLineItems { get; set; } = new();
}

public sealed class SalesQuotePrefillLine
{
    public int? SellLineItemId { get; set; }
    public int? QuotationId { get; set; }
    public int? QuotationOptionId { get; set; }
    public int? BuyLineItemId { get; set; }
    public int OrgId { get; set; }
    public int? ChargeItemId { get; set; }
    public string? HsnCode { get; set; }
    public string? ChargeDescription { get; set; }
    public int? ApplyPerId { get; set; }
    public string? ApplyPerName { get; set; }
    public int? SellCurrencyId { get; set; }
    public string? SellCurrencyCode { get; set; }
    public decimal? SellExRate { get; set; }
    public decimal? SellRate { get; set; }
    public decimal? SellQuantity { get; set; }
    public decimal? SellSubtotal { get; set; }
    public int? SellTaxId { get; set; }
    public decimal? SellTaxPercent { get; set; }
    public decimal? SellTaxAmount { get; set; }
    public decimal? SellTotalAmount { get; set; }
    public int? BuyCurrencyId { get; set; }
    public string? BuyCurrencyCode { get; set; }
    public decimal? BuyExRate { get; set; }
    public decimal? BuyRate { get; set; }
    public decimal? BuyQuantity { get; set; }
    public decimal? BuySubtotal { get; set; }
    public int? BuyTaxId { get; set; }
    public decimal? BuyTaxPercent { get; set; }
    public decimal? BuyTaxAmount { get; set; }
    public decimal? BuyTotalAmount { get; set; }
    public int? InvoiceToId { get; set; }
    public string? InvoiceToName { get; set; }
    public int? BillToId { get; set; }
    public string? BillToName { get; set; }
    public bool IsAllIn { get; set; }
    public string? Remarks { get; set; }
    public int? SortOrder { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedBy { get; set; }
    public DateTime? DeletedDate { get; set; }
    public int? LocalCurrencyId { get; set; }
    public decimal? SellSubtotalLocal { get; set; }
    public decimal? SellTaxAmountLocal { get; set; }
    public decimal? SellTotalAmountLocal { get; set; }
    public decimal? BuySubtotalLocal { get; set; }
    public decimal? BuyTaxAmountLocal { get; set; }
    public decimal? BuyTotalAmountLocal { get; set; }
}
