using System;

namespace RepoDbApi.Models;

public class CargoMintCostSheetCharge
{
    public int CargoID { get; set; }
    public int? CostSheetID { get; set; }
    public string? OperationsGroup { get; set; }
    public string? ChargeName { get; set; }
    public string? ChargeDescription { get; set; }
    public string? ApplyPer { get; set; }
    public int? LineNumber { get; set; }

    // BUY (Expense)
    public decimal? BuyQty { get; set; }
    public decimal? BuyRate { get; set; }
    public string? BuyCurrency { get; set; }
    public decimal? BuyExRate { get; set; }
    public decimal? BuyAmount { get; set; }
    public decimal? BuyTaxPercent { get; set; }

    // SELL (Income)
    public decimal? SellQty { get; set; }
    public decimal? SellRate { get; set; }
    public string? SellCurrency { get; set; }
    public decimal? SellExRate { get; set; }
    public decimal? SellAmount { get; set; }
    public decimal? SellTaxPercent { get; set; }

    // Paying parties
    public int? PayToID { get; set; }
    public int? InvoiceToID { get; set; }
    public string? PayToName { get; set; }
    public string? InvoiceToName { get; set; }

    // Created / Modified by
    public string? CreatedByName { get; set; }
    public string? ModifiedByName { get; set; }

    // Profit
    public decimal? Profit { get; set; }
}
