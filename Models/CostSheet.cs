namespace GeneratedModels
{
    public class CostSheet
    {
        public int CostSheetID { get; set; }
        public int CargoID { get; set; }
        public string CostSheetType { get; set; }
        public string CostSheetStatus { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public string? ModifiedByName { get; set; }
        public DateTime? DateModified { get; set; }
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
        public int? CostCategoryID { get; set; }
    }
}
