namespace GeneratedModels
{
    public class ExchangeRates
    {
        public int ExchangeRateID { get; set; }
        public DateTime ExchangeDateFrom { get; set; }
        public DateTime ExchangeDateTo { get; set; }
        public int BaseCurrencyID { get; set; }
        public int ConvertedCurrencyID { get; set; }
        public bool isDeleted { get; set; }
        public int? OfficeID { get; set; }
        public int? CountryID { get; set; }
        public decimal? ExchangeBuyingRate { get; set; }
        public decimal? ExchangeSellingRate { get; set; }
        public decimal? ExchangeBuyingRateInverse { get; set; }
        public decimal? ExchangeSellingInverse { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DeletedDate { get; set; }
    }
}
