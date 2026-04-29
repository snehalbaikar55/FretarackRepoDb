namespace GeneratedModels
{
    public class VendorBillLineItems
    {
        public int ChargeItemID { get; set; }
        public int VendorBillLineItemID { get; set; }
        public int VendorBillID { get; set; }
        public int SortOrder { get; set; }
        public bool isDeleted { get; set; }
        public string ServiceCode { get; set; }
        public string ApplyPer { get; set; }
        public int? ChargeID { get; set; }
        public string ChargeDescription { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? Rate { get; set; }
        public int? CurrencyID { get; set; }
        public string CurrencyCode { get; set; }
        public int? ExRateID { get; set; }
        public decimal? ExRate { get; set; }
        public int? TaxRateID { get; set; }
        public decimal? TaxPercent { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? TaxableAmount { get; set; }
        public decimal? NonTaxableAmount { get; set; }
        public decimal? ExpectedAmount { get; set; }
        public decimal? RealizedAmount { get; set; }
        public decimal? TotalAmountLocalCurrency { get; set; }
        public decimal? TaxAmountLocalCurrency { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
