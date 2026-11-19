using GeneratedModels;

namespace RepoDbApi.Repositories;

public sealed class MappedSalesQuoteHeader : SalesQuote_New
{
    public int? CargoMintSalesQuoteID { get; set; }
    public int? CargoMintCompanyID { get; set; }
    public int? CargoMintContactID { get; set; }
    public int? CargoMintSalesPersonID { get; set; }
    public int? CargoMintPreparedBy { get; set; }
    public int? CargoMintCreatedBy { get; set; }
    public int? CargoMintModifiedBy { get; set; }
    public int? CargoMintDeletedBy { get; set; }
    public int? CargoMintIncoTermID { get; set; }
    public int? CargoMintSalesQuoteCurrencyID { get; set; }
}

public sealed class MappedSalesQuoteDetail : SalesQuoteDetails_New
{
    public int? CargoMintSalesQuoteDetailID { get; set; }
    public int? CargoMintPOLID { get; set; }
    public int? CargoMintPODID { get; set; }
    public int? CargoMintCarrierID { get; set; }
}

public sealed class MappedSalesQuoteCharge : SalesQuoteCharges_New
{
    public int? CargoMintCreatedBy { get; set; }
    public int? CargoMintModifiedBy { get; set; }
    public int? CargoMintDeletedBy { get; set; }
    public int? CargoMintSQChargeID { get; set; }
    public int? CargoMintChargeItemID { get; set; }
    public int? CargoMintCurrencyID { get; set; }
    public int? CargoMintPayingPartyID { get; set; }
    public int? CargoMintTaxRateID { get; set; }
}

public sealed class SalesQuoteServiceModeReference
{
    public int? CargoMintServiceTypeId { get; set; }
    public int? CargoMintTransportModeId { get; set; }
}

public sealed class SalesQuoteCurrencyReference
{
    public int CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}

public sealed class SalesQuoteNamedReference
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class SalesQuoteTaxReference
{
    public int TaxGroupId { get; set; }
    public decimal? TotalRate { get; set; }
}

public sealed class SalesQuotePrefillReferences
{
    public int? ServiceTypeId { get; set; }
    public string? ServiceTypeName { get; set; }
    public int? ModeId { get; set; }
    public int? DirectionId { get; set; }
    public int? LocalCurrencyId { get; set; }
    public int? CommodityId { get; set; }
    public List<SalesQuoteCurrencyReference> Currencies { get; set; } = new();
    public List<SalesQuoteNamedReference> Companies { get; set; } = new();
    public List<SalesQuoteNamedReference> Units { get; set; } = new();
    public List<SalesQuoteTaxReference> Taxes { get; set; } = new();
}
