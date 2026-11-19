using System.Reflection;
using System.Text.Json;

namespace RepoDbApi.Contracts;

// Table rows contain only the supplied SQL columns, serialized as CargoMint camelCase.
public sealed class SalesQuoteTableRows
{
    public List<Dictionary<string, object?>> SalesQuotations { get; } = new();
    public List<Dictionary<string, object?>> SalesQuotationServiceDetails { get; } = new();
    public List<Dictionary<string, object?>> SalesQuotationRateOptions { get; } = new();
    public List<Dictionary<string, object?>> SalesQuotationLineItems { get; } = new();
    public List<SalesQuoteTableRowLink> RowLinks { get; } = new();

    public static SalesQuoteTableRows From(SalesQuotePrefillData quotation)
    {
        var result = new SalesQuoteTableRows();
        result.SalesQuotations.Add(Project(quotation, HeaderColumns));
        foreach (var service in quotation.SalesQuotationServiceDetails)
        {
            var serviceRow = result.SalesQuotationServiceDetails.Count;
            result.SalesQuotationServiceDetails.Add(Project(service, ServiceColumns));
            foreach (var option in service.SalesQuotationRateOptions)
            {
                var optionRow = result.SalesQuotationRateOptions.Count;
                result.SalesQuotationRateOptions.Add(Project(option, OptionColumns));
                var lineRows = new List<int>();
                foreach (var line in option.SalesQuotationLineItems)
                {
                    lineRows.Add(result.SalesQuotationLineItems.Count);
                    result.SalesQuotationLineItems.Add(Project(line, LineColumns));
                }
                result.RowLinks.Add(new(0, serviceRow, optionRow, lineRows));
            }
        }
        return result;
    }

    private static Dictionary<string, object?> Project(object source, string[] columns)
    {
        var row = new Dictionary<string, object?>();
        foreach (var column in columns)
        {
            var property = source.GetType().GetProperty(column, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"Missing table response field: {column}");
            row[JsonNamingPolicy.CamelCase.ConvertName(column)] = property.GetValue(source);
        }
        return row;
    }

    private static readonly string[] HeaderColumns =
    [
        "QuotationId", "EnquiryId", "SalesPersonId", "QuotationNo", "QuotationDate",
        "ClosedDate", "ValidityDate", "QuotationStatus", "CurrencyId", "TermsConditions",
        "OverallComments", "TotalApprovedCostRate", "TotalFinalSalesRate", "TotalProfit", "TotalProfitPercentage",
        "CreatedBy", "CreatedDate", "UpdatedBy", "UpdatedDate", "IsDeleted",
        "DeletedBy", "DeletedDate", "CustomerId", "ContactId", "IncoTermId",
        "CommodityId", "orgId", "NominationType", "ReceivedDate", "ExRate",
        "LocalCurrencyId", "TotalApprovedCostRateLocal", "TotalFinalSalesRateLocal", "TotalProfitLocal", "PresentationMode",
        "ShowTax", "ShowTerms", "ShowCarrier", "ChargeGroupingMode", "PresentationCombinationsJson",
        "PresentationLockedDate"
    ];

    private static readonly string[] ServiceColumns =
    [
        "QuotationServiceId", "QuotationId", "EnquiryServiceId", "ServiceTypeId", "ServiceLabel",
        "ApprovedCostRate", "FinalSalesRate", "ServiceStatus", "ServiceNotes", "BuyingRateId",
        "RateRequestId", "CreatedBy", "CreatedDate", "UpdatedBy", "UpdatedDate",
        "IsDeleted", "DeletedBy", "DeletedDate", "SalesQuoteServiceRefNo", "TransportMode",
        "TransportDirection", "PolId", "PodId", "ServiceLocationId", "orgId",
        "TransportModeId", "TransportDirectionId", "CurrencyId", "ExRate", "LocalCurrencyId",
        "ApprovedCostRateLocal", "FinalSalesRateLocal", "IsPrimary"
    ];

    private static readonly string[] OptionColumns =
    [
        "QuotationOptionId", "QuotationServiceId", "OptionLabel", "VendorId", "ApprovedCostRate",
        "FinalSalesRate", "OptionStatus", "OptionNotes", "BuyingRateId", "CreatedDate",
        "CreatedBy", "UpdatedDate", "UpdatedBy", "IsDeleted", "DeletedBy",
        "DeletedDate", "CarrierId", "orgId", "CurrencyId", "ExRate",
        "LocalCurrencyId", "CustomerOptionLabel", "CustomerTag"
    ];

    private static readonly string[] LineColumns =
    [
        "SellLineItemId", "QuotationId", "QuotationOptionId", "ChargeItemId", "HsnCode",
        "ChargeDescription", "ApplyPerId", "SellCurrencyId", "SellExRate", "SellRate",
        "SellQuantity", "SellSubtotal", "SellTaxId", "SellTaxPercent", "SellTaxAmount",
        "SellTotalAmount", "BuyCurrencyId", "BuyExRate", "BuyRate", "BuyQuantity",
        "BuySubtotal", "BuyTaxId", "BuyTaxPercent", "BuyTaxAmount", "BuyTotalAmount",
        "BuyLineItemId", "InvoiceToId", "BillToId", "IsAllIn", "Remarks",
        "SortOrder", "CreatedBy", "CreatedDate", "UpdatedBy", "UpdatedDate",
        "IsDeleted", "DeletedBy", "DeletedDate", "orgId", "LocalCurrencyId",
        "SellSubtotalLocal", "SellTaxAmountLocal", "SellTotalAmountLocal", "BuySubtotalLocal", "BuyTaxAmountLocal",
        "BuyTotalAmountLocal"
    ];

}

// New target IDs stay NULL. These zero-based links preserve row associations.
public sealed record SalesQuoteTableRowLink(int QuotationRowIndex, int ServiceRowIndex,
    int RateOptionRowIndex, List<int> LineItemRowIndices);
