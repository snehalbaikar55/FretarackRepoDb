namespace GeneratedModels
{
    public class SalesCostCategory
    {
        public int CostCategoryID { get; set; }
        public int SalesQuoteID { get; set; }
        public string CostCategoryType { get; set; }
        public string CostCategoryStatus { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? BuyingRatesIncharge { get; set; }
        public int? SellingRatesIncharge { get; set; }
        public int? AssignedTo { get; set; }
        public string VerifiedBy { get; set; }
        public decimal? BuyExRateUSD { get; set; }
        public decimal? BuyExRateEUR { get; set; }
        public decimal? BuyExRateTL { get; set; }
        public decimal? BuyExRateINR { get; set; }
        public decimal? SellExRateUSD { get; set; }
        public decimal? SellExRateEUR { get; set; }
        public decimal? SellExRateTL { get; set; }
        public decimal? SellExRateINR { get; set; }
        public DateTime? ExchangeRateDate { get; set; }
    }
}
