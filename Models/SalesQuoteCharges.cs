namespace GeneratedModels
{
    public class SalesQuoteCharges
    {
        public int ChargeGroupID { get; set; }
        public int ChargeItemID { get; set; }
        public string ChargeDescription { get; set; }
        public bool isExpense { get; set; }
        public int ApplicableToCompanyID { get; set; }
        public int SalesQuoteChargeID { get; set; }
        public int SalesQuoteServiceID { get; set; }
        public int SalesQuoteID { get; set; }
        public bool isDeleted { get; set; }
        public string ChargePer { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public int? Quantity { get; set; }
        public int? UnitID { get; set; }
        public decimal? Rate { get; set; }
        public decimal? TaxPercent { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? TotalAmount { get; set; }
    }
}
